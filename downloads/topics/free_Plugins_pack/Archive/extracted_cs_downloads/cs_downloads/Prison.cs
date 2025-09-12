using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Prison", "rustmods.ru", "1.0.7")]
    public class Prison : CovalencePlugin
    {
        #region Lang
        protected override void LoadDefaultMessages()
        {

            //English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Free"] = "You are free!",
                ["Offline"] = "Prisoner is offline",
                ["TimeLeft"] = "Prison time left: ",
                ["Suicide"] = "You cant suicide while in prison!",
                ["Argument"] = "Argument missing!",
                ["Jail"] = "Jail ",
                ["delLiberation"] = "Liberation point ",
                ["Created"] = " created!",
                ["Liberation"] = "liberation point is set!",
                ["Available"] = "Jails available!",
                ["lib"] = "Liberation points available!",
                ["DeletedAll"] = "All jails deleted!",
                ["Deleted"] = " deleted!",
                ["NotExisting"] = "Jail not existing!",
                ["libNotExisting"] = "Liberation point not existing!",
                ["Command"] = "Command: /sendjail [player name] [jail name] [minutes]",
                ["SendJail"] = "You have been send to jail for ",
                ["Minutes"] = " minute(s)!",
                ["OfflineOrJail"] = "Player already in jail or offline",
                ["Input"] = "Wrong input playername or jail",
                ["FreePoint"] = "liberation point already existing!",
                ["JailExisting"] = "Jail already exists!",
                ["occupied"] = "Jail is occupied!",
                ["Positive"] = "Only positive numbers",
                ["NumbersOnly"] = "Input only numbers",
                ["SendToJail"] = "Player has been send to jail",
                ["associated"] = "\nand all associated jails to this liberation point",
                ["NotCuffed"] = "Player is not Handcuffed",
                ["DeletedAlllib"] = "Deleted all liberation points!",
                ["->"] = "->",
                ["<-"] = "<-",

            }, this);

            //German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Free"] = "Du bist frei!",
                ["Offline"] = "Gefangener ist offline",
                ["TimeLeft"] = "Gefängnis Zeit übrig: ",
                ["Suicide"] = "Du kannst im Gefängnis kein Selbstmord begehen!",
                ["Argument"] = "Argument fehlt!",
                ["Jail"] = "Gefängnis ",
                ["delLiberation"] = "Befreiungspunkt ",
                ["Created"] = " erstellt!",
                ["associated"] = "\nund alle mit diesem Befreiungspunkt verbundenen Gefängnisse",
                ["libNotExisting"] = "Befreiungspunkt nicht vorhanden!",
                ["Liberation"] = "Befreiungspunkt wurde gesetzt!",
                ["Available"] = "Gefängnisse verfügbar!",
                ["lib"] = "Befreiungspunkte verfügbar!",
                ["DeletedAll"] = "Alle Gefängnisse gelöscht!",
                ["DeletedAlllib"] = "Alle Befreiungspunkte gelöscht!",
                ["Deleted"] = " gelöscht!",
                ["NotExisting"] = "Gefängnis nicht vorhanden!",
                ["Command"] = "Command: /sendjail [Spieler Name] [Gefängnis Name] [Minuten]",
                ["SendJail"] = "Du wurdest ins Gefängnis geschickt für ",
                ["Minutes"] = " Minute(n)!",
                ["OfflineOrJail"] = "Der Spieler ist offline oder schon im Gefängnis",
                ["Input"] = "Falsche Eingabe Spieler Name oder Gefängnis Name",
                ["FreePoint"] = "Befreiungspunkt bereits vorhanden!",
                ["JailExisting"] = "Das Gefängnis existiert bereits",
                ["occupied"] = "Gefängnis ist besetzt!",
                ["Positive"] = "Nur positive Zahlen",
                ["NumbersOnly"] = "Nur Zahlen eingeben",
                ["SendToJail"] = "Spieler wurde ins Gefängnis geschickt",
                ["NotCuffed"] = "Spieler is nicht in Handschellen",
                ["->"] = "->",
                ["<-"] = "<-",
            }, this, "de");
        }
        #endregion

        #region Init
        [PluginReference] Plugin LockMeUp;
        char[] separators = new char[] { ',' };
        char[] trim = new char[] { '(', ')' };
        ArrayList Prisoners = new ArrayList();
        public Dictionary<string, string> Jail = new Dictionary<string, string>();
        List<Item> collect = new List<Item>();
        public Dictionary<ulong, Timer> CraftTimer = new Dictionary<ulong, Timer>();
        public string fx = "assets/bundled/prefabs/fx/notice/item.select.fx.prefab";
        private static string perm = "prison.use";
        #endregion

        #region Data
        DynamicConfigFile dataFile;
        class DynamicConfigFile
        {
            public Dictionary<string, string> jails = new Dictionary<string, string>();
            public Dictionary<string, string> cellLiberationPoint = new Dictionary<string, string>();
            public Dictionary<string, string> liberationPoints = new Dictionary<string, string>();

        }


        private Configuration config;

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = Configuration.CreateConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);


        class Configuration
        {
            [JsonProperty("Use_LockMeUp_Only_Send_To_Jail_If_Player_Handcuffed")]
            public bool useLockMeUp { get; set; }


            public static Configuration CreateConfig()
            {

                return new Configuration
                {
                    useLockMeUp = false,

                };
            }
        }



        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadDefaultMessages();
            permission.RegisterPermission(perm, this);
        }

        void Loaded()
        {
            dataFile = Interface.Oxide.DataFileSystem.ReadObject<DynamicConfigFile>("Prison");
            Interface.Oxide.DataFileSystem.WriteObject("Prison", dataFile);
            foreach (var data in dataFile.jails)
            {
                if(data.Key == "free")
                {

                }
                else
                {
                    Jail.Add(data.Key, "(free)");

                }
                
            }
        }
        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Prison", dataFile);
        }

        void Unload()
        {
            SaveData();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "panel");
                CuiHelper.DestroyUi(player, "counter");
                DestroyTimer(player.userID);
                if (player.inventory.containerMain.IsLocked())
                {
                    player.inventory.containerWear.SetLocked(false);
                    player.inventory.containerMain.SetLocked(false);
                    player.inventory.containerBelt.SetLocked(false);
                    player.inventory.containerWear.Take(collect, 935692442, 1);
                    player.inventory.containerWear.Take(collect, 237239288, 1);
                    player.inventory.containerBelt.capacity = 6;
                    collect.Clear();
                }
            }
            
         
        }

     
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var player = entity.ToPlayer();
            if(player)
            {
                if (Prisoners.Contains(player.UserIDString.ToLower()))
                {            
                    info.damageTypes.ScaleAll(0);
                    return false;
                }
                else
                {
                    return null;
                }
                
            } 
            return null;
        }


        #endregion

        #region Commands


        [Command("jail")]
        private void OpenPanelJail(IPlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.Id.ToString(), perm))
            {
                var bplayer = (BasePlayer)player.Object;
                PrisonPanel(bplayer);
            }
        }


        [Command("setjail")]
        private void SetJail(IPlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.Id.ToString(), perm))
            {
                if (args.Length == 2)
                {
                    if (!Jail.Keys.Contains(args[0]))
                    {
                        if (dataFile.liberationPoints.ContainsKey(args[1]))
                        {
                            player.Reply(lang.GetMessage("Jail", this) + args[0] + lang.GetMessage("Created", this));
                            dataFile.jails.Add(args[0], player.Position().ToString());
                            dataFile.cellLiberationPoint.Add(args[0], args[1]);
                            Jail.Add(args[0], "(free)");
                            SaveData();
                        }
                        else
                        {
                            player.Reply(lang.GetMessage("libNotExisting", this));

                        }
                        
                    }
                    else
                    {
                        player.Reply(lang.GetMessage("JailExisting", this));
                    }
                }
                else
                {
                    player.Reply(lang.GetMessage("Argument", this));
                }
            }
           
            

        }
        [Command("setfree")]
        private void SetFree(IPlayer player, string command, string[] args)
        {
            
            if (permission.UserHasPermission(player.Id.ToString(), perm))
            {
                if(args.Length == 1)
                {
                    if (dataFile.liberationPoints.Keys.Contains(args[0]))
                    {
                        player.Reply(lang.GetMessage("FreePoint", this));
                    }
                    else
                    {
                        player.Reply(lang.GetMessage("Liberation", this));
                        dataFile.liberationPoints.Add(args[0], player.Position().ToString());
                        SaveData();
                    }
                }
                else
                {
                    player.Reply(lang.GetMessage("Argument", this));
                }
               


            }
        }
        [Command("getjail")]
        private void GetJail(IPlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.Id.ToString(), perm))
            {
                var jailList = string.Join("\n", Jail.ToArray()).Replace("[", "").Replace("]", "");
                player.Reply(lang.GetMessage("Available", this) + "\n--------------------\n" + jailList + "\n--------------------");
            }
        }
        [Command("getliberation")]
        private void GetLiberationPoint(IPlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.Id.ToString(), perm))
            {
                var jailList = string.Join("\n", dataFile.liberationPoints.ToArray()).Replace("[", "").Replace("]", "");
                player.Reply(lang.GetMessage("lib", this) + "\n--------------------\n" + jailList + "\n--------------------");
            }
        }
        [Command("deljail")]
        private void DeletJail(IPlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.Id.ToString(), perm))
            {
                if (args.Length == 1)
                {
                    
                    if (args[0].Equals("all", StringComparison.OrdinalIgnoreCase))
                    {
                        dataFile.jails.Clear();
                        dataFile.cellLiberationPoint.Clear();
                        Jail.Clear();
                        SaveData();
                        player.Reply(lang.GetMessage("DeletedAll", this));
                    }
                    else
                    {
                        try
                        {
                            player.Reply(lang.GetMessage("Jail", this) + args[0] + lang.GetMessage("Deleted", this));
                            dataFile.jails.Remove(args[0]);
                            dataFile.cellLiberationPoint.Remove(args[0]);
                            Jail.Remove(args[0]);
                            SaveData();
                        }
                        catch
                        {
                            player.Reply(lang.GetMessage("NotExisting", this));
                        }

                    }
                }
                else
                {
                    player.Reply(lang.GetMessage("Argument", this));
                }
                
                
            }
        }

        [Command("delliberation")]
        private void DeletLiberationPoint(IPlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.Id.ToString(), perm))
            {
                if (args.Length == 1)
                {

                    if (args[0].Equals("all", StringComparison.OrdinalIgnoreCase))
                    {
                        dataFile.liberationPoints.Clear();
                        dataFile.jails.Clear();
                        Jail.Clear();
                        dataFile.cellLiberationPoint.Clear();
                        SaveData();
                        player.Reply(lang.GetMessage("DeletedAlllib", this) + lang.GetMessage("associated", this));
                    }
                    else
                    {
                       
                        if (dataFile.liberationPoints.ContainsKey(args[0]))
                        {
                             dataFile.liberationPoints.Remove(args[0]);
                                
                            try
                            {
                                foreach (KeyValuePair<string, string> cell in dataFile.cellLiberationPoint)
                                {
                                    if (cell.Value.Equals(args[0]))
                                    {
                                        dataFile.jails.Remove(cell.Key);
                                        Jail.Remove(cell.Key);
                                        dataFile.cellLiberationPoint.Remove(cell.Key);
                                    }
                                }
                            }
                            catch
                            {
                               
                            }
                            player.Reply(lang.GetMessage("delLiberation", this) + args[0] + lang.GetMessage("Deleted", this) + lang.GetMessage("associated", this));
                            SaveData();
                        }
                        else
                        {
                            player.Reply(lang.GetMessage("libNotExisting", this));
                        }
                           

                    }
                }
                else
                {
                    player.Reply(lang.GetMessage("Argument", this));
                }
            }
        }

        


        [Command("freejail")]
        private void FreeJail(IPlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.Id.ToString(), perm))
            {
           
                if (args.Length != 1)
                {
                    player.Reply(lang.GetMessage("Argument", this));

                }
                else
                {
                    try
                    {

                        string jailCell = args[0];
                        var liberation = dataFile.cellLiberationPoint[jailCell];
                        var Position = dataFile.liberationPoints[liberation];
                        var PositionNew1 = Position.ToString().Trim(trim);
                        var PositionNew2 = PositionNew1.Split(separators);
                        float x = float.Parse(PositionNew2[0], CultureInfo.InvariantCulture.NumberFormat);
                        float y = float.Parse(PositionNew2[1], CultureInfo.InvariantCulture.NumberFormat);
                        float z = float.Parse(PositionNew2[2], CultureInfo.InvariantCulture.NumberFormat);
                        string name;
                        Jail.TryGetValue(jailCell, out name);
                        string trimmedName = name.Trim(trim);
                        foreach (var bplayer in BasePlayer.allPlayerList)
                        {
                            if (bplayer.displayName.Equals(trimmedName, StringComparison.OrdinalIgnoreCase))
                            {
                                Jail.Remove(args[0]);
                                Jail.Add(args[0], "(free)");
                                var cplayer = bplayer.IPlayer;
                                bplayer.ChatMessage(lang.GetMessage("Free", this));
                                bplayer.inventory.containerWear.SetLocked(false);
                                bplayer.inventory.containerMain.SetLocked(false);
                                bplayer.inventory.containerBelt.SetLocked(false);
                                bplayer.inventory.containerWear.Take(collect, 935692442, 1);
                                bplayer.inventory.containerWear.Take(collect, 237239288, 1);
                                collect.Clear();
                                bplayer.inventory.containerBelt.capacity = 6;
                                Prisoners.Remove(bplayer.UserIDString.ToLower());
                                cplayer.Teleport(x, y, z);
                                CuiHelper.DestroyUi(bplayer, "panel");
                                CuiHelper.DestroyUi(bplayer, "counter");
                                DestroyTimer(bplayer.userID);
                            }
                        }
                    }
                    catch
                    {
                        player.Reply(lang.GetMessage("Input", this));
                    }
                }
            }
        }

      

        [Command("prison.GetName")]
        private void GetName(IPlayer player, string command, string[] args)
        {
            var bplayer = (BasePlayer)player.Object;
              if (bplayer == null || args == null) return;

            switch (args[0])
            {
                case "openpanel2":
                    CuiHelper.DestroyUi(bplayer, "PrisonPanel");
                    PrisonPanel2(bplayer, args[1]);
                    break;
                case "openpanel3":
                    CuiHelper.DestroyUi(bplayer, "PrisonPanel2");
                    PrisonPanel3(bplayer, args[1], args[2]);
                    break;
                case "Send":
                    SendJailAutom(player, args[1], args[2], args[3]);
                    CuiHelper.DestroyUi(bplayer, "PrisonPanel3");
                    break;
            }
        }

        [Command("PM.TogglePlayerGroup")]
        private void PMTogglePlayerGroup(IPlayer player, string command, string[] args)
        {
            var bplayer = (BasePlayer)player.Object;
            if (bplayer == null || args == null) return;

            int page = Convert.ToInt16(args[0]);
            CuiHelper.DestroyUi(bplayer, "PrisonPanel");
            PrisonPanel(bplayer, page);
        }

 

        [Command("PM.ToggleCellGroud")]
        private void ToggleCellGroud(IPlayer player, string command, string[] args)
        {
            var bplayer = (BasePlayer)player.Object;
            if (bplayer == null || args == null) return;

            int page = Convert.ToInt16(args[1]);
            CuiHelper.DestroyUi(bplayer, "PrisonPanel2");
            PrisonPanel2(bplayer, args[0] , page);
        }

        #endregion

        #region Functions



        private void SendJailAutom(IPlayer player, string name, string jail, string timecount)
        {
            
            if(config.useLockMeUp == true)
            {
                var dplayer = (BasePlayer)player.Object;
                


                    string value;
                    Jail.TryGetValue(jail, out value);
                    try
                    {
                        float newtime = float.Parse(timecount);
                        if (newtime > 0)
                        {
                            if (!Prisoners.Contains(name.ToLower()) && CheckIfOnline(name) == true)
                            {

                                if (value == "(free)")
                                {
                                    var tshirt = global::ItemManager.FindItemDefinition("tshirt.long");
                                    var pants = global::ItemManager.FindItemDefinition("pants");
                                    string playerName = name;
                                    string JailName = jail;
                                    var Position = dataFile.jails[JailName];
                                    var PositionNew1 = Position.ToString().Trim(trim);
                                    var PositionNew2 = PositionNew1.Split(separators);
                                    float x = float.Parse(PositionNew2[0], CultureInfo.InvariantCulture.NumberFormat);
                                    float y = float.Parse(PositionNew2[1], CultureInfo.InvariantCulture.NumberFormat);
                                    float z = float.Parse(PositionNew2[2], CultureInfo.InvariantCulture.NumberFormat);
                                    foreach (var bplayer in BasePlayer.activePlayerList)
                                    {
                                        if (bplayer.UserIDString.Equals(playerName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            if ((Boolean)LockMeUp?.Call("IsRestrained", bplayer) == true)
                                            {
                                                LockMeUp?.Call("UnRestrainPlayer", bplayer);
                                                Jail.Remove(JailName);
                                                Jail.Add(JailName, "(" + bplayer.displayName + ")");
                                                var cplayer = bplayer.IPlayer;
                                                bplayer.ChatMessage(lang.GetMessage("SendJail", this) + timecount + lang.GetMessage("Minutes", this));
                                                bplayer.inventory.containerWear.SetLocked(true);
                                                bplayer.inventory.containerMain.SetLocked(true);
                                                bplayer.inventory.containerBelt.SetLocked(true);
                                                bplayer.inventory.containerBelt.capacity = 0;
                                                bplayer.inventory.containerWear.AddItem(tshirt, 1, 2354721675);
                                                bplayer.inventory.containerWear.AddItem(pants, 1, 2354729871);
                                            Prisoners.Add(bplayer.UserIDString.ToLower());
                                                
                                                cplayer.Teleport(x, y, z);
                                                var craftTime = newtime * 60f;
                                                var time = 0;
                                                if (CraftTimer.ContainsKey(bplayer.userID)) CraftTimer.Remove(bplayer.userID);
                                                panel(bplayer);
                                                var newTime = craftTime - time;
                                                counter(bplayer, convertTime(newTime));
                                                CraftTimer.Add(bplayer.userID, timer.Every(1f, () =>
                                                {
                                                    time++;
                                                    var timeNew = craftTime - time;
                                                    counter(bplayer, convertTime(timeNew));
                                                    if (time >= craftTime)
                                                    {
                                                        string liberationPoint;
                                                        dataFile.cellLiberationPoint.TryGetValue(JailName, out liberationPoint);
                                                        CuiHelper.DestroyUi(bplayer, "panel");
                                                        CuiHelper.DestroyUi(bplayer, "counter");
                                                        freejailautom(playerName, liberationPoint, JailName);
                                                        DestroyTimer(bplayer.userID);
                                                    }
                                                }));
                                            }
                                             else
                                             {
                                          
                                                RunEffect(dplayer.transform.position, fx, dplayer);
                                                InfoPanel(dplayer, "NotCuffed");
                                                player.Reply(lang.GetMessage("NotCuffed", this));
                                            
                                             }

                                        }
                                    }

                                }
                                else
                                {
                                    RunEffect(dplayer.transform.position, fx, dplayer);
                                    InfoPanel(dplayer, "occupied");
                                    player.Reply(lang.GetMessage("occupied", this));
                                }

                            }
                            else
                            {
                                RunEffect(dplayer.transform.position, fx, dplayer);
                                InfoPanel(dplayer, "OfflineOrJail");
                                player.Reply(lang.GetMessage("OfflineOrJail", this));
                            }
                        }
                        else
                        {
                            RunEffect(dplayer.transform.position, fx, dplayer);
                            InfoPanel(dplayer, "Positive");
                            player.Reply(lang.GetMessage("Positive", this));
                        }
                    }
                    catch
                    {
                        RunEffect(dplayer.transform.position, fx, dplayer);
                        InfoPanel(dplayer, "NumbersOnly");
                        player.Reply(lang.GetMessage("NumbersOnly", this));
                    }

            }
            else
            {
                var dplayer = (BasePlayer)player.Object;
                string value;
                    Jail.TryGetValue(jail, out value);
                    try
                    {
                        float newtime = float.Parse(timecount);
                        if (newtime > 0)
                        {
                            if (!Prisoners.Contains(name.ToLower()) && CheckIfOnline(name) == true)
                            {

                                if (value == "(free)")
                                {
                                    var tshirt = global::ItemManager.FindItemDefinition("tshirt.long");
                                    var pants = global::ItemManager.FindItemDefinition("pants");
                                    string playerName = name;
                                    string JailName = jail;
                                    var Position = dataFile.jails[JailName];
                                    var PositionNew1 = Position.ToString().Trim(trim);
                                    var PositionNew2 = PositionNew1.Split(separators);
                                    float x = float.Parse(PositionNew2[0], CultureInfo.InvariantCulture.NumberFormat);
                                    float y = float.Parse(PositionNew2[1], CultureInfo.InvariantCulture.NumberFormat);
                                    float z = float.Parse(PositionNew2[2], CultureInfo.InvariantCulture.NumberFormat);
                                    foreach (var bplayer in BasePlayer.activePlayerList)
                                    {
                                        if (bplayer.UserIDString.Equals(playerName, StringComparison.OrdinalIgnoreCase))
                                        {
                                          
                                            Jail.Remove(JailName);
                                            Jail.Add(JailName, "(" + bplayer.displayName + ")");
                                            var cplayer = bplayer.IPlayer;
                                            bplayer.ChatMessage(lang.GetMessage("SendJail", this) + timecount + lang.GetMessage("Minutes", this));
                                            bplayer.inventory.containerWear.SetLocked(true);
                                            bplayer.inventory.containerMain.SetLocked(true);
                                            bplayer.inventory.containerBelt.SetLocked(true);
                                            bplayer.inventory.containerBelt.capacity = 0;                                          
                                            bplayer.inventory.containerWear.AddItem(tshirt, 1, 2354721675);
                                            bplayer.inventory.containerWear.AddItem(pants, 1, 2354729871);
                                            Prisoners.Add(bplayer.UserIDString.ToLower());
                                            cplayer.Teleport(x, y, z);
                                            var craftTime = newtime * 60f;
                                            var time = 0;
                                            if (CraftTimer.ContainsKey(bplayer.userID)) CraftTimer.Remove(bplayer.userID);
                                            panel(bplayer);
                                            var newTime = craftTime - time;
                                            counter(bplayer, convertTime(newTime));
                                            CraftTimer.Add(bplayer.userID, timer.Every(1f, () =>
                                            {
                                                time++;
                                                var timeNew = craftTime - time;
                                                counter(bplayer, convertTime(timeNew));
                                                if (time >= craftTime)
                                                {
                                                    string liberationPoint;
                                                    dataFile.cellLiberationPoint.TryGetValue(JailName, out liberationPoint);
                                                    CuiHelper.DestroyUi(bplayer, "panel");
                                                    CuiHelper.DestroyUi(bplayer, "counter");
                                                    freejailautom(playerName, liberationPoint, JailName);
                                                    DestroyTimer(bplayer.userID);
                                                }
                                            }));

                                        }
                                    }

                                }
                                else
                                {
                                    RunEffect(dplayer.transform.position, fx, dplayer);
                                    InfoPanel(dplayer, "occupied");
                                    player.Reply(lang.GetMessage("occupied", this));
                                }

                            }
                            else
                            {
                                RunEffect(dplayer.transform.position, fx, dplayer);
                                InfoPanel(dplayer, "OfflineOrJail");
                                player.Reply(lang.GetMessage("OfflineOrJail", this));
                            }
                        }
                        else
                        {
                            RunEffect(dplayer.transform.position, fx, dplayer);
                            InfoPanel(dplayer, "Positive");
                            player.Reply(lang.GetMessage("Positive", this));
                        }
                    }
                    catch
                    {
                        RunEffect(dplayer.transform.position, fx, dplayer);
                        InfoPanel(dplayer, "NumbersOnly");
                        player.Reply(lang.GetMessage("NumbersOnly", this));
                    }
 
              
            }
            
                
            
        }


        private static void RunEffect(Vector3 position, string prefab, BasePlayer player = null)
        {
            var effect = new Effect();
            effect.Init(Effect.Type.Generic, position, Vector3.zero);
            effect.pooledString = prefab;

            if (player != null)
            {
                EffectNetwork.Send(effect, player.net.connection);
            }
            else
            {
                EffectNetwork.Send(effect);
            }
        }


        private string convertTime(float totalSeconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(totalSeconds);
            var newTime = time.ToString("hh':'mm':'ss");
            return newTime;
        }

        private bool CheckIfOnline(string playername)
        {
            bool online = false;
            foreach (var onlineplayer in BasePlayer.activePlayerList)
            {

                if (onlineplayer.UserIDString.Equals(playername, StringComparison.OrdinalIgnoreCase))
                {
                    online = true;
                }


            }
            return online;
        }

        void DestroyTimer(ulong id)
        {
            if (CraftTimer.ContainsKey(id))
            {
                if (!CraftTimer[id].Destroyed) CraftTimer[id].Destroy();
                CraftTimer.Remove(id);
            }
        }



        private void freejailautom(string playerName, string JailName, string freeJail)
        {
            
                var Position = dataFile.liberationPoints[JailName];
                var PositionNew1 = Position.ToString().Trim(trim);
                var PositionNew2 = PositionNew1.Split(separators);
                float x = float.Parse(PositionNew2[0], CultureInfo.InvariantCulture.NumberFormat);
                float y = float.Parse(PositionNew2[1], CultureInfo.InvariantCulture.NumberFormat);
                float z = float.Parse(PositionNew2[2], CultureInfo.InvariantCulture.NumberFormat);
                foreach (var bplayer in BasePlayer.allPlayerList)
                {
                    if (bplayer.UserIDString.Equals(playerName, StringComparison.OrdinalIgnoreCase))
                    {
                        Jail.Remove(freeJail);
                        Jail.Add(freeJail, "(free)");
                        var cplayer = bplayer.IPlayer;
                        bplayer.ChatMessage(lang.GetMessage("Free", this));
                        bplayer.inventory.containerWear.SetLocked(false);
                        bplayer.inventory.containerMain.SetLocked(false);
                        bplayer.inventory.containerBelt.SetLocked(false);
                        bplayer.inventory.containerBelt.capacity = 6;
                        bplayer.inventory.containerWear.Take(collect, 935692442, 1);
                        bplayer.inventory.containerWear.Take(collect, 237239288, 1);
                        collect.Clear();
                        Prisoners.Remove(bplayer.UserIDString.ToLower());
                        cplayer.Teleport(x, y, z);
                    Puts(playerName + "befreit");
                    }
                }
           

        }
        #endregion

        #region UI
        void panel(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-114.858 -279.428", OffsetMax = "115.552 -248.572" }
            }, "Overlay", "panel");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                FadeOut = 1,
                Image = { Color = "0 0 0 0.5", Material = "assets/content/ui/uibackgroundblur.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-115.206 -15.428", OffsetMax = "115.204 15.428" }
            }, "panel", "PanelBack");

            CuiHelper.DestroyUi(player, "panel");
            CuiHelper.AddUi(player, container);
        }
        void counter(BasePlayer player, string timeleft)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = "counter",
                Parent = "panel",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("TimeLeft",this) + "<color=orange>"+timeleft+"</color>", Font = "robotocondensed-bold.ttf", FontSize = 17, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-115.206 -15.428", OffsetMax = "115.204 15.428" }
                }
            });

            CuiHelper.DestroyUi(player, "counter");
            CuiHelper.AddUi(player, container);
        }






        


        void PrisonPanel(BasePlayer player, int page = 1)
        {
            CuiHelper.DestroyUi(player, "PrisonPanel2");
            CuiHelper.DestroyUi(player, "PrisonPanel3");
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0.85", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1"}
            }, "Overlay", "PrisonPanel");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                FadeOut = 1,
                Image = { Color = "0 0 0 0.95", Material = "assets/content/ui/uibackgroundblur.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-335.206 -205.428", OffsetMax = "325.204 205.428" }
            }, "PrisonPanel", "PrisonPanelBack");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                FadeOut = 1,
                Image = { Color = "0 0 0 1"},
                RectTransform = { AnchorMin = "0.5 0.760", AnchorMax = "0.5 0.760", OffsetMin = "-335.206 -19.428", OffsetMax = "325.204 19.428" }
            }, "PrisonPanel", "PrisonPanelBackBack");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.708 0.727", AnchorMax = "0.76 0.79" },
                Button = { Color = "0 0 0 0", Close = "PrisonPanel" },
                Text =
                {
                    Text = "×", Font = "robotocondensed-regular.ttf", FontSize = 36, Align = TextAnchor.MiddleCenter,
                    Color = "0.56 0.58 0.64 1.00"
                }
            },"PrisonPanel", "ButtonClose");

            container.Add(new CuiElement
            {
                Name = "PrisonText",
                Parent = "PrisonPanel",
                Components = {
                    new CuiTextComponent { Text = "Send to Prison", Font = "robotocondensed-bold.ttf", FontSize = 17, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.499 0.727", AnchorMax = "0.499 0.79", OffsetMin = "-115.206 -15.428", OffsetMax = "115.204 15.428" }
                }
            });

            int pos1 = 48 - (page * 48), quantity = 0;
            float top = 0.720f;
            float bottom = 0.690f;
            int playercounter = 0;
            foreach (var bplayer in BasePlayer.activePlayerList)
            {
                pos1++;
                quantity++;
                if (pos1 > 0 && pos1 < 49)
                {
                   
                    playercounter++;
                    if (playercounter <= 12)
                    {
                     
                        container.Add(new CuiButton
                        {

                            RectTransform = { AnchorMin = $"0.268 {bottom}", AnchorMax = $"0.350 {top}", OffsetMin = "-25.206 -1.428", OffsetMax = "25.204 1.428" },
                            Button = { Color = "0.56 0.58 0.64 1.00", Command = $"prison.GetName openpanel2 {bplayer.UserIDString}" },
                            Text =
                    {
                    Text = bplayer.displayName, Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                    }
                        }, "PrisonPanel", "ButtonGetName");
                        top = top - 0.040f;
                        bottom = bottom - 0.040f;
                        if (playercounter == 12)
                        {
                            top = 0.720f;
                            bottom = 0.690f;
                        }
                    }
                    else if (playercounter <= 24)
                    {

                        container.Add(new CuiButton
                        {

                            RectTransform = { AnchorMin = $"0.392 {bottom}", AnchorMax = $"0.474 {top}", OffsetMin = "-25.206 -1.428", OffsetMax = "25.204 1.428" },
                            Button = { Color = "0.56 0.58 0.64 1.00", Command = $"prison.GetName openpanel2 {bplayer.UserIDString}" },
                            Text =
                    {
                    Text = bplayer.displayName, Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                    }
                        }, "PrisonPanel", "ButtonGetName");
                        top = top - 0.040f;
                        bottom = bottom - 0.040f;
                        if (playercounter == 24)
                        {
                            top = 0.720f;
                            bottom = 0.690f;
                        }
                    }
                    else if (playercounter <= 36)
                    {

                        container.Add(new CuiButton
                        {

                            RectTransform = { AnchorMin = $"0.516 {bottom}", AnchorMax = $"0.598 {top}", OffsetMin = "-25.206 -1.428", OffsetMax = "25.204 1.428" },
                            Button = { Color = "0.56 0.58 0.64 1.00", Command = $"prison.GetName openpanel2 {bplayer.UserIDString}" },
                            Text =
                    {
                    Text = bplayer.displayName, Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                    }
                        }, "PrisonPanel", "ButtonGetName");
                        top = top - 0.040f;
                        bottom = bottom - 0.040f;
                        if (playercounter == 36)
                        {
                            top = 0.720f;
                            bottom = 0.690f;
                        }
                    }
                    else if (playercounter <= 48)
                    {

                        container.Add(new CuiButton
                        {

                            RectTransform = { AnchorMin = $"0.640 {bottom}", AnchorMax = $"0.722 {top}", OffsetMin = "-25.206 -1.428", OffsetMax = "25.204 1.428" },
                            Button = { Color = "0.56 0.58 0.64 1.00", Command = $"prison.GetName openpanel2 {bplayer.UserIDString}" },
                            Text =
                    {
                    Text = bplayer.displayName, Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                    }
                        }, "PrisonPanel", "ButtonGetName");
                        top = top - 0.040f;
                        bottom = bottom - 0.040f;
                        if (playercounter == 48)
                        {
                            top = 0.720f;
                            bottom = 0.690f;
                        }
                    }
                }

            }
            if (quantity > (page * 48))
                container.Add(new CuiButton { Button = { Command = $"PM.TogglePlayerGroup {page + 1}", Color = "0.56 0.58 0.64 1.00" }, RectTransform = { AnchorMin = "0.50 0.22", AnchorMax = "0.55 0.24" }, Text = { Text = lang.GetMessage("->", this), FontSize = 11, Align = TextAnchor.MiddleCenter }, }, "PrisonPanel");

            if (page > 1)
                container.Add(new CuiButton { Button = { Command = $"PM.TogglePlayerGroup {page - 1}", Color = "0.56 0.58 0.64 1.00" }, RectTransform = { AnchorMin = "0.44 0.22", AnchorMax = "0.49 0.24" }, Text = { Text = lang.GetMessage("<-", this), FontSize = 11, Align = TextAnchor.MiddleCenter }, }, "PrisonPanel");


            CuiHelper.DestroyUi(player, "PrisonPanel");
            CuiHelper.AddUi(player, container);
        }



        void PrisonPanel2(BasePlayer player, string playername = "", int page = 1)
        {
            string name = playername;
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0.85", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, "Overlay", "PrisonPanel2");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                FadeOut = 1,
                Image = { Color = "0 0 0 0.95", Material = "assets/content/ui/uibackgroundblur.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-335.206 -205.428", OffsetMax = "325.204 205.428" }
            }, "PrisonPanel2", "PrisonPanelBack2");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                FadeOut = 1,
                Image = { Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.760", AnchorMax = "0.5 0.760", OffsetMin = "-335.206 -19.428", OffsetMax = "325.204 19.428" }
            }, "PrisonPanel2", "PrisonPanelBackBack2");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.708 0.727", AnchorMax = "0.76 0.79" },
                Button = { Color = "0 0 0 0", Close = "PrisonPanel2" },
                Text =
                {
                    Text = "×", Font = "robotocondensed-regular.ttf", FontSize = 36, Align = TextAnchor.MiddleCenter,
                    Color = "0.56 0.58 0.64 1.00"
                }
            }, "PrisonPanel2", "ButtonClose2");

            container.Add(new CuiElement
            {
                Name = "PrisonText2",
                Parent = "PrisonPanel2",
                Components = {
                    new CuiTextComponent { Text = "Jail Cells", Font = "robotocondensed-bold.ttf", FontSize = 17, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.499 0.727", AnchorMax = "0.499 0.79", OffsetMin = "-115.206 -15.428", OffsetMax = "115.204 15.428" }
                }
            });
            int pos1 = 48 - (page * 48), quantity = 0;
            float top = 0.720f;
            float bottom = 0.690f;
            int playercounter = 0;
            foreach (var jail in Jail)
            {

                pos1++;
                quantity++;
                if (pos1 > 0 && pos1 < 49)
                {
                    playercounter++;
                    if (playercounter <= 12)
                    {

                        container.Add(new CuiButton
                        {

                            RectTransform = { AnchorMin = $"0.268 {bottom}", AnchorMax = $"0.350 {top}", OffsetMin = "-25.206 -1.428", OffsetMax = "25.204 1.428" },
                            Button = { Color = "0.56 0.58 0.64 1.00", Command = $"prison.GetName openpanel3 {name} {jail.Key}" },
                            Text =
                {
                    Text = jail.Key+ " " + jail.Value, Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
                        }, "PrisonPanel2", "ButtonGetNam2");
                        top = top - 0.040f;
                        bottom = bottom - 0.040f;
                        if (playercounter == 12)
                        {
                            top = 0.720f;
                            bottom = 0.690f;
                        }
                    }
                    else if (playercounter <= 24)
                    {
                        container.Add(new CuiButton
                        {

                            RectTransform = { AnchorMin = $"0.392 {bottom}", AnchorMax = $"0.474 {top}", OffsetMin = "-25.206 -1.428", OffsetMax = "25.204 1.428" },
                            Button = { Color = "0.56 0.58 0.64 1.00", Command = $"prison.GetName openpanel3 {name} {jail.Key}" },
                            Text =
                {
                    Text = jail.Key+ " " + jail.Value, Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
                        }, "PrisonPanel2", "ButtonGetNam2");
                        top = top - 0.040f;
                        bottom = bottom - 0.040f;

                        if (playercounter == 24)
                        {
                            top = 0.720f;
                            bottom = 0.690f;
                        }
                    }
                    else if (playercounter <= 36)
                    {
                        container.Add(new CuiButton
                        {

                            RectTransform = { AnchorMin = $"0.516 {bottom}", AnchorMax = $"0.598 {top}", OffsetMin = "-25.206 -1.428", OffsetMax = "25.204 1.428" },
                            Button = { Color = "0.56 0.58 0.64 1.00", Command = $"prison.GetName openpanel3 {name} {jail.Key}" },
                            Text =
                {
                    Text = jail.Key+ " " + jail.Value, Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
                        }, "PrisonPanel2", "ButtonGetNam2");
                        top = top - 0.040f;
                        bottom = bottom - 0.040f;

                        if (playercounter == 36)
                        {
                            top = 0.720f;
                            bottom = 0.690f;
                        }
                    }
                    else if (playercounter <= 48)
                    {
                        container.Add(new CuiButton
                        {

                            RectTransform = { AnchorMin = $"0.640 {bottom}", AnchorMax = $"0.722 {top}", OffsetMin = "-25.206 -1.428", OffsetMax = "25.204 1.428" },
                            Button = { Color = "0.56 0.58 0.64 1.00", Command = $"prison.GetName openpanel3 {name} {jail.Key}" },
                            Text =
                {
                    Text = jail.Key+ " " + jail.Value, Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
                        }, "PrisonPanel2", "ButtonGetNam2");
                        top = top - 0.040f;
                        bottom = bottom - 0.040f;

                        if (playercounter == 48)
                        {
                            top = 0.720f;
                            bottom = 0.690f;
                        }
                    }
                }
               
            }

            if (quantity > (page * 48))
                container.Add(new CuiButton { Button = { Command = $"PM.ToggleCellGroud {name} {page + 1}", Color = "0.56 0.58 0.64 1.00" }, RectTransform = { AnchorMin = "0.50 0.22", AnchorMax = "0.55 0.24" }, Text = { Text = lang.GetMessage("->", this), FontSize = 11, Align = TextAnchor.MiddleCenter }, }, "PrisonPanel2");

            if (page > 1)
                container.Add(new CuiButton { Button = { Command = $"PM.ToggleCellGroud {name} {page - 1}", Color = "0.56 0.58 0.64 1.00" }, RectTransform = { AnchorMin = "0.44 0.22", AnchorMax = "0.49 0.24" }, Text = { Text = lang.GetMessage("<-", this), FontSize = 11, Align = TextAnchor.MiddleCenter }, }, "PrisonPanel2");


            CuiHelper.DestroyUi(player, "PrisonPanel2");
            CuiHelper.AddUi(player, container);
        }



        void PrisonPanel3(BasePlayer player, string playername, string key)
        {
            string name = playername;
            string newkey = key;
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0.85", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, "Overlay", "PrisonPanel3");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                FadeOut = 1,
                Image = { Color = "0 0 0 0.95", Material = "assets/content/ui/uibackgroundblur.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-335.206 -205.428", OffsetMax = "325.204 205.428" }
            }, "PrisonPanel3", "PrisonPanelBack3");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                FadeOut = 1,
                Image = { Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.760", AnchorMax = "0.5 0.760", OffsetMin = "-335.206 -19.428", OffsetMax = "325.204 19.428" }
            }, "PrisonPanel3", "PrisonPanelBackBack3");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.708 0.727", AnchorMax = "0.76 0.79" },
                Button = { Color = "0 0 0 0", Close = "PrisonPanel3" },
                Text =
                {
                    Text = "×", Font = "robotocondensed-regular.ttf", FontSize = 36, Align = TextAnchor.MiddleCenter,
                    Color = "0.56 0.58 0.64 1.00"
                }
            }, "PrisonPanel3", "ButtonClose3");

            container.Add(new CuiElement
            {
                Name = "PrisonText3",
                Parent = "PrisonPanel3",
                Components = {
                    new CuiTextComponent { Text = "Set Jail Time", Font = "robotocondensed-bold.ttf", FontSize = 17, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.499 0.727", AnchorMax = "0.499 0.79", OffsetMin = "-115.206 -15.428", OffsetMax = "115.204 15.428" }
                }
            });

            container.Add(new CuiPanel
            {
                RectTransform =
                        {
                            AnchorMin = "0.7 0.8", AnchorMax = "0.7 0.8",
                            OffsetMin = "-400 -255",
                            OffsetMax = "-110 -155"
                        },
                Image =
                        {
                            Color = "0.14 0.14 0.14 1"
                        }
            }, "PrisonPanel3", "Input");

            container.Add(new CuiLabel
            {
                RectTransform =
                        {
                            AnchorMin = "0 0.5", AnchorMax = "1 1",
                            OffsetMin = "25 0", OffsetMax = "0 0"
                        },
                Text =
                        {
                            Text = "Enter time in minutes:",
                            Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 14,
                            Color = "0.61 0.61 0.61 1"
                        }
            }, "Input");

            container.Add(new CuiElement
            {
                Parent = "Input",
                Components =
                        {
                            new CuiInputFieldComponent
                            {
                                FontSize = 16,
                                Align = TextAnchor.UpperLeft,
                                Font = "robotocondensed-bold.ttf",
                                Command = $"prison.GetName Send {name} {newkey}",
                                Color = "1 1 1 1",
                                CharsLimit = 9
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 0.5",
                                OffsetMin = "25 0", OffsetMax = "0 0"
                            }
                        }
            });

            container.Add(new CuiButton
            {

                RectTransform = { AnchorMin = $"0.462 0.290", AnchorMax = $"0.549 0.320", OffsetMin = "-30.206 -1.428", OffsetMax = "30.204 1.428" },
                Button = { Color = "0.56 0.58 0.64 1.00", Command = $"prison.Send Send" },
                Text =
                {
                    Text = "Send", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, "PrisonPanel3", "Button10");

            

            CuiHelper.DestroyUi(player, "PrisonPanel3");
            CuiHelper.AddUi(player, container);
            
        }



        private void InfoPanel(BasePlayer player, string txt)
        {

            CuiHelper.DestroyUi(player, "InfoPanel");

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.719 0.975", AnchorMax = "0.8 1" }
            }, "Overlay", "InfoPanel");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                FadeOut = 1,
                Image = { Color = "0 0 0 0.5", Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = 0.25f },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-70.206 -25.428", OffsetMax = "300.204 5.428" }
            }, "InfoPanel", "InfoPanelBack");

            container.Add(new CuiElement
            {
                Name = "InfoText",
                Parent = "InfoPanel",
                FadeOut = 1,
                Components = {
                    new CuiTextComponent { Text = $"<color=red>{lang.GetMessage(txt, this)}</color>", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FadeIn = 0.25f },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-75.206 -25.428", OffsetMax = "300.204 5.428" }
                }
            });

            CuiHelper.AddUi(player, container);
            timer.Once(5f, () => { CuiHelper.DestroyUi(player, "InfoPanelBack"); CuiHelper.DestroyUi(player, "InfoText"); });
        }

        #endregion
    }
}
