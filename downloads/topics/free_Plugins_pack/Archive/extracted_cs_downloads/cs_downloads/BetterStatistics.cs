using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Database;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Better Statistics", "Billy Joe", "1.1.8")]
    [Description("A plugin that syncs players statistics to a MySQL database.")]
    public class BetterStatistics : CovalencePlugin
    {
        #region Config
        static Configuration config;
        public class DBInfo
        {
            [JsonProperty(PropertyName = "Host IP")] public string sql_host;
            [JsonProperty(PropertyName = "Port")] public int sql_port;
            [JsonProperty(PropertyName = "Database name")] public string sql_db;
            [JsonProperty(PropertyName = "Username")] public string sql_user;
            [JsonProperty(PropertyName = "Password")] public string sql_passwword;
        }
        public class Configuration
        {
            [JsonProperty(PropertyName = "Developer Mode (Debug Messages)")] public bool developerMode;
            [JsonProperty(PropertyName = "MySQL Database Connection Info")] public DBInfo connectionInfo;
            [JsonProperty(PropertyName = "Server Prefix (For tables Ex: (prefix)_stats_wipe, (prefix)_stats_overall)")] public string serverPrefix;
            [JsonProperty(PropertyName = "Save Interval (Mins) (0 = Disabled)")] public int saveInterval;
            [JsonProperty(PropertyName = "Use In-Game UI")] public bool useIngameUI;
            [JsonProperty(PropertyName = "Exclude NPC Type Deaths from death count?")] public bool excludeNPCDeaths;
            [JsonProperty(PropertyName = "Only count kills on players that have a stash on them?")] public bool onlyStashKills;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    developerMode = false,
                    connectionInfo = new DBInfo
                    {
                        sql_host = "localhost",
                        sql_port = 3306,
                        sql_user = "root",
                        sql_passwword = "",
                        sql_db = "betterstatistics"
                    },
                    serverPrefix = "main",
                    saveInterval = 10,
                    useIngameUI = true,
                    excludeNPCDeaths = true,
                    onlyStashKills = false
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Language
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UpdatedPlayerStats"] = "Updated {0} stats.",
                ["NoDatabaseConnection"] = "Database connection failed. Please check your credentials and try again.",
                ["PlayerNotFound"] = "Player not found in wipe stats table.",
                ["PlayerNotFoundOverall"] = "Player not found in overall stats table, these stats are filled when the server wipes.",
                ["UITitle"] = "Better Statistics",
                ["UIWipeButton"] = "WIPE",
                ["UIOverallButton"] = "OVERALL",
                ["UIExitButton"] = "EXIT",
                ["UIHead"] = "Head",
                ["UITorso"] = "Torso",
                ["UILeftArm"] = "Left Arm",
                ["UIRightArm"] = "Right Arm",
                ["UILeftLeg"] = "Left Leg",
                ["UIRightLeg"] = "Right Leg",
                ["UILeftFoot"] = "Left Foot",
                ["UIRightFoot"] = "Right Foot",
                ["UIPlayerStats"] = "Player Stats",
                ["UIConnects"] = "Connects",
                ["UIPlaytime"] = "Playtime",
                ["UIKills"] = "Kills",
                ["UIDeaths"] = "Deaths",
                ["UISuicides"] = "Suicides",
                ["UIWounded"] = "Wounded",
                ["UIKDRatio"] = "K/D Ratio",
                ["UIAccuracy"] = "Accuracy",
                ["UISatchels"] = "Satchels",
                ["UIC4"] = "C4",
                ["UIRockets"] = "Rockets",
                ["UITCsDestroyed"] = "TCs Destroyed",
                ["UINPCKills"] = "NPC Kills",
                ["UIChickens"] = "Chickens",
                ["UIBoars"] = "Boars",
                ["UIStags"] = "Stags",
                ["UIHorses"] = "Horses",
                ["UIWolves"] = "Wolves",
                ["UIBears"] = "Bears",
                ["UIScientists"] = "Scientists",
                ["UIHelicopters"] = "Helicopters",
                ["UIBradleys"] = "Bradleys",
                ["UIWeaponKills"] = "Weapon Kills",
                ["UINextPage"] = "NEXT PAGE",
                ["UIPreviousPage"] = "PREV PAGE"
            }, this);
        }

        private string GetLang(string langKey, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this), args);
        }
        #endregion

        #region Classes
        public class PlayerStats
        {
            public string name = string.Empty;
            public int connections = 0;
            public int playtime = 0;
            public DateTime connected = DateTime.Now;
            public int kills = 0;
            public int deaths = 0;
            public int bfired = 0;
            public int suicides = 0;
            public int wounded = 0;
            public int rocketsfired = 0;
            public int c4thrown = 0;
            public int satchelsthrown = 0;
            public int tcsdestroyed = 0;
            public NPCStats npcStats = new NPCStats();
            public WeaponStats weaponStats = new WeaponStats();
            public BodyParts hits = new BodyParts();
        }

        public class NPCStats
        {
            public int chickens = 0;
            public int boars = 0;
            public int deers = 0;
            public int horses = 0;
            public int wolves = 0;
            public int bears = 0;
            public int scientists = 0;
            public int helicopters = 0;
            public int bradleys = 0;
        };

        public class WeaponStats
        {
            public int ak47 = 0;
            public int lr300 = 0;
            public int m39 = 0;
            public int sar = 0;
            public int hmlmg = 0;
            public int m249 = 0;
            public int bolt = 0;
            public int l96 = 0;
            public int mp5 = 0;
            public int thompson = 0;
            public int custom = 0;
            public int doublebarrel = 0;
            public int pump = 0;
            public int spaz12 = 0;
            public int m92 = 0;
            public int python = 0;
            public int prototype = 0;
            public int semipistol = 0;
            public int revolver = 0;
            public int nailgun = 0;
            public int waterpipe = 0;
            public int eoka = 0;
            public int compound = 0;
            public int crossbow = 0;
            public int bow = 0;
        };

        public class BodyParts
        {
            public int head = 0;
            public int torso = 0;
            public int lefthand = 0;
            public int righthand = 0;
            public int leftleg = 0;
            public int rightleg = 0;
            public int leftfoot = 0;
            public int rightfoot = 0;
        };

        public class Weapon
        {
            public string WeaponName = "";
            public string WeaponIcon = "";
        }



        List<Weapon> weapons = new List<Weapon>()
        {
            new Weapon
            {
                WeaponName = "AK",
                WeaponIcon = "https://i.imgur.com/DX6M8IM.png"
            },
            new Weapon
            {
                WeaponName = "LR",
                WeaponIcon = "https://i.imgur.com/EqSj6od.png"
            },
            new Weapon
            {
                WeaponName = "M39",
                WeaponIcon = "https://i.imgur.com/pj5eK7W.png"
            },
            new Weapon
            {
                WeaponName = "M249",
                WeaponIcon = "https://i.imgur.com/5ebpq7S.png"
            },
            new Weapon
            {
                WeaponName = "HMLMG",
                WeaponIcon = "https://i.imgur.com/WcruPRb.png"
            },
            new Weapon
            {
                WeaponName = "SAR",
                WeaponIcon = "https://i.imgur.com/n1N0y5R.png"
            },
            new Weapon
            {
                WeaponName = "L9",
                WeaponIcon = "https://i.imgur.com/WG8tWwT.png"
            },
            new Weapon
            {
                WeaponName = "BOLT",
                WeaponIcon = "https://i.imgur.com/SC8tLG6.png"
            },
            new Weapon
            {
                WeaponName = "MP5",
                WeaponIcon = "https://i.imgur.com/trJCQry.png"
            },
            new Weapon
            {
                WeaponName = "THOMPSON",
                WeaponIcon = "https://i.imgur.com/J4cAEf8.png"
            },
            new Weapon
            {
                WeaponName = "CUSTOMSMG",
                WeaponIcon = "https://i.imgur.com/KqK3ffK.png"
            },
            new Weapon
            {
                WeaponName = "PUMP",
                WeaponIcon = "https://i.imgur.com/7LwdMsG.png"
            },
            new Weapon
            {
                WeaponName = "DOUBLE",
                WeaponIcon = "https://i.imgur.com/eXT2fkS.png"
            },
            new Weapon
            {
                WeaponName = "SPAZ12",
                WeaponIcon = "https://i.imgur.com/PKfrRmz.png"
            },
            new Weapon
            {
                WeaponName = "M92",
                WeaponIcon = "https://i.imgur.com/nLn1Kyx.png"
            },
            new Weapon
            {
                WeaponName = "PYTHON",
                WeaponIcon = "https://i.imgur.com/BlJKetO.png"
            },
            new Weapon
            {
                WeaponName = "PROTOTYPE",
                WeaponIcon = "https://i.imgur.com/Gt88ceW.png"
            },
            new Weapon
            {
                WeaponName = "SEMIPISTOL",
                WeaponIcon = "https://i.imgur.com/OCH6rsz.png"
            },
            new Weapon
            {
                WeaponName = "REVOLVER",
                WeaponIcon = "https://i.imgur.com/NRlB8Ds.png"
            },
            new Weapon
            {
                WeaponName = "NAILGUN",
                WeaponIcon = "https://i.imgur.com/9pM6PJX.png"
            },
            new Weapon
            {
                WeaponName = "WATERPIPE",
                WeaponIcon = "https://i.imgur.com/fHKeAhR.png"
            },
            new Weapon
            {
                WeaponName = "EOKA",
                WeaponIcon = "https://i.imgur.com/Uh6bya5.png"
            },
            new Weapon
            {
                WeaponName = "COMPOUND",
                WeaponIcon = "https://i.imgur.com/pU8CXpI.png"
            },
            new Weapon
            {
                WeaponName = "CROSSBOW",
                WeaponIcon = "https://i.imgur.com/e6ukhQV.png"
            },
            new Weapon
            {
                WeaponName = "BOW",
                WeaponIcon = "https://i.imgur.com/lfbpVqR.png"
            }
        };

        private int GetAmount(WeaponStats wStats, string weapon)
        {
            switch (weapon)
            {
                case "AK":
                    return wStats.ak47;
                case "LR":
                    return wStats.lr300;
                case "M39":
                    return wStats.m39;
                case "M249":
                    return wStats.m249;
                case "HMLMG":
                    return wStats.hmlmg;
                case "SAR":
                    return wStats.sar;
                case "L9":
                    return wStats.l96;
                case "BOLT":
                    return wStats.bolt;
                case "MP5":
                    return wStats.mp5;
                case "THOMPSON":
                    return wStats.thompson;
                case "CUSTOMSMG":
                    return wStats.custom;
                case "PUMP":
                    return wStats.pump;
                case "DOUBLE":
                    return wStats.doublebarrel;
                case "SPAZ12":
                    return wStats.spaz12;
                case "M92":
                    return wStats.m92;
                case "PYTHON":
                    return wStats.python;
                case "PROTOTYPE":
                    return wStats.prototype;
                case "SEMIPISTOL":
                    return wStats.semipistol;
                case "REVOLVER":
                    return wStats.revolver;
                case "NAILGUN":
                    return wStats.nailgun;
                case "WATERPIPE":
                    return wStats.waterpipe;
                case "EOKA":
                    return wStats.eoka;
                case "COMPOUND":
                    return wStats.compound;
                case "CROSSBOW":
                    return wStats.crossbow;
                case "BOW":
                    return wStats.bow;
            }

            return -1;
        }
        #endregion

        #region Defines
        [PluginReference] private Plugin ImageLibrary;
        Dictionary<string, PlayerStats> playerStats = new Dictionary<string, PlayerStats>();
        Dictionary<string, PlayerStats> playerStatsOverall = new Dictionary<string, PlayerStats>();
        Core.MySql.Libraries.MySql SQL = Interface.Oxide.GetLibrary<Core.MySql.Libraries.MySql>();
        Connection connection;
        PlayerStats stats;
        private const string TableExists = "SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @0";
        BasePlayer HeliAttacker = null;
        DamageType[] suicideDamageTypes = { DamageType.Suicide, DamageType.Radiation, DamageType.RadiationExposure, DamageType.Poison, DamageType.Hunger, DamageType.Thirst, DamageType.Fall, DamageType.Drowned };
        #endregion

        #region Hooks
        void Loaded()
        {
            connection = SQL.OpenDb(config.connectionInfo.sql_host, config.connectionInfo.sql_port, config.connectionInfo.sql_db, config.connectionInfo.sql_user, config.connectionInfo.sql_passwword, this);
            connection.Con.Open();

            if (connection == null || connection.Con == null)
            {
                Puts(GetLang("NoDatabaseConnection"));
                return;
            }
        }
        void Unload()
        {
            connection?.Con?.Dispose();
            config = null;
        }
        void OnNewSave(string filename)
        {
            if (config.developerMode) Puts("Moving wipe table to overall due to new save file.");
            var query = Sql.Builder.Append($"CALL `wipeTime`('{config.serverPrefix}')");
            SQL.Query(query, connection, created => { });
        }
        void OnServerSave()
        {
            if (ServerMgr.Instance.Restarting)
            {
                if (config.developerMode) Puts("Ran OnServerSave since server is restarting.");
                SaveAllStats();
            }
        }
        void OnServerInitialized(bool initial)
        {
            ServerMgr.Instance.StartCoroutine(Startup());

            ImageLibrary?.Call("AddImage", "https://i.imgur.com/BtYyVsQ.png", "PlayerImage");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/hvnWown.png", "GrungeBackground");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/FZHGHUc.png", "ConnectionsIcon");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/DRdUVK6.png", "PlaytimeIcon");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/0Bmhgma.png", "DeathsIcon");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/5Sbd7Ir.png", "KDRIcon");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/85BOzMc.png", "AccuracyIcon");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/ycz4eyv.png", "WoundedIcon");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/ttLSDHS.png", "SuicidesIcon");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/bZA0E92.png", "SatchelsIcon");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/4ix2QDq.png", "C4Icon");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/m0VKILI.png", "RocketsIcon");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/n2b0FlG.png", "TCIcon");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/iXPMAhu.png", "ChickenIcon");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/suelqfc.png", "BoarIcon");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/VIszPrw.png", "DeerIcon");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/nErC1Ht.png", "HorseIcon");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/LGka2Zw.png", "WolveIcon");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/0lYoss1.png", "BearIcon");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/09sY8jQ.png", "ScientistIcon");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/q0KkIgi.png", "HelicopterIcon");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/RwqvbVv.png", "BradleyIcon");

            foreach (var weapon in weapons)
                ImageLibrary?.Call("AddImage", weapon.WeaponIcon, weapon.WeaponName);

            if (config.saveInterval != 0)
                timer.Every(config.saveInterval * 60, () =>
                {
                    SaveAllStats();
                });

            if (!initial)
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    if (playerStats.TryGetValue(player.UserIDString, out stats))
                        stats.connected = DateTime.Now;
            }
        }
        void OnPlayerConnected(BasePlayer player)
        {
            if (connection == null || connection.Con == null) return;

            if (playerStats.TryGetValue(player.UserIDString, out stats))
            {
                stats.connected = DateTime.Now;
                stats.connections += 1;
            }

            Sql query = Sql.Builder.Append($"SELECT steamid FROM {config.serverPrefix}_stats_wipe WHERE steamid = '{player.userID}'");
            SQL.Query(query, connection, list =>
            {
                if (list.Count == 0)
                {
                    Sql insquery = Sql.Builder.Append($"INSERT INTO {config.serverPrefix}_stats_wipe (steamid, name) VALUES ({player.UserIDString}, '{RemoveSpecialCharacters(player.displayName)}')");
                    SQL.Insert(insquery, connection);

                    playerStats.Add(player.UserIDString, new PlayerStats
                    {
                        name = RemoveSpecialCharacters(player.displayName),
                        connections = 1,
                        playtime = 0,
                        kills = 0,
                        deaths = 0,
                        bfired = 0,
                        suicides = 0,
                        wounded = 0,
                        c4thrown = 0,
                        satchelsthrown = 0,
                        rocketsfired = 0,
                        tcsdestroyed = 0,
                        npcStats = new NPCStats
                        {
                            chickens = 0,
                            boars = 0,
                            deers = 0,
                            horses = 0,
                            wolves = 0,
                            bears = 0,
                            scientists = 0,
                            helicopters = 0,
                            bradleys = 0
                        },
                        weaponStats = new WeaponStats
                        {
                            ak47 = 0,
                            lr300 = 0,
                            m39 = 0,
                            sar = 0,
                            m249 = 0,
                            hmlmg = 0,
                            l96 = 0,
                            bolt = 0,
                            mp5 = 0,
                            thompson = 0,
                            custom = 0,
                            pump = 0,
                            doublebarrel = 0,
                            spaz12 = 0,
                            m92 = 0,
                            python = 0,
                            prototype = 0,
                            semipistol = 0,
                            revolver = 0,
                            nailgun = 0,
                            waterpipe = 0,
                            eoka = 0,
                            compound = 0,
                            crossbow = 0,
                            bow = 0
                        },
                        hits = new BodyParts
                        {
                            head = 0,
                            torso = 0,
                            lefthand = 0,
                            righthand = 0,
                            leftleg = 0,
                            rightleg = 0,
                            leftfoot = 0,
                            rightfoot = 0
                        },
                    });
                }
            });
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (reason.Equals("Server Restarting", StringComparison.OrdinalIgnoreCase)) return;
            SavePlayerStats(player, true);
        }
        void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            if (!playerStats.TryGetValue(player.UserIDString, out stats)) return;
            if (suicideDamageTypes.Contains(player.lastDamage)) stats.suicides++;
            else
            {
                if (!(config.excludeNPCDeaths && info?.Initiator is BaseNpc))
                    stats.deaths++;
            }

            BasePlayer attacker = info?.InitiatorPlayer;
            if (attacker == null || attacker == player || (attacker.currentTeam > 0 && attacker.currentTeam == player.currentTeam) || (config.onlyStashKills && !player.HasPlayerFlag(BasePlayer.PlayerFlags.DisplaySash))) return;

            if (!playerStats.TryGetValue(attacker.UserIDString, out stats)) return;

            stats.kills++;
            Item weaponItem = info.Weapon?.GetItem();
            if (weaponItem == null) return;
            switch (weaponItem.info.shortname)
            {
                case "rifle.ak":
                case "rifle.ak.ice":
                    stats.weaponStats.ak47++;
                    break;
                case "rifle.lr300":
                    stats.weaponStats.lr300++;
                    break;
                case "rifle.m39":
                    stats.weaponStats.m39++;
                    break;
                case "rifle.semiauto":
                    stats.weaponStats.sar++;
                    break;
                case "lmg.m249":
                    stats.weaponStats.m249++;
                    break;
                case "hmlmg":
                    stats.weaponStats.hmlmg++;
                    break;
                case "rifle.l96":
                    stats.weaponStats.l96++;
                    break;
                case "rifle.bolt":
                    stats.weaponStats.bolt++;
                    break;
                case "smg.mp5":
                    stats.weaponStats.mp5++;
                    break;
                case "smg.thompson":
                    stats.weaponStats.thompson++;
                    break;
                case "smg.2":
                    stats.weaponStats.custom++;
                    break;
                case "shotgun.double":
                    stats.weaponStats.doublebarrel++;
                    break;
                case "shotgun.pump":
                    stats.weaponStats.pump++;
                    break;
                case "shotgun.spas12":
                    stats.weaponStats.spaz12++;
                    break;
                case "pistol.m92":
                    stats.weaponStats.m92++;
                    break;
                case "pistol.python":
                    stats.weaponStats.python++;
                    break;
                case "pistol.semiauto":
                    stats.weaponStats.semipistol++;
                    break;
                case "pistol.prototype17":
                    stats.weaponStats.prototype++;
                    break;
                case "pistol.revolver":
                    stats.weaponStats.revolver++;
                    break;
                case "pistol.nailgun":
                    stats.weaponStats.nailgun++;
                    break;
                case "shotgun.waterpipe":
                    stats.weaponStats.waterpipe++;
                    break;
                case "pistol.eoka":
                    stats.weaponStats.eoka++;
                    break;
                case "bow.compound":
                    stats.weaponStats.compound++;
                    break;
                case "crossbow":
                    stats.weaponStats.crossbow++;
                    break;
                case "bow.hunting":
                    stats.weaponStats.bow++;
                    break;
            }
        }
        void OnEntityDeath(BuildingPrivlidge entity, HitInfo info)
        {
            BasePlayer attacker = info?.InitiatorPlayer;
            if (attacker == null) return;

            if (playerStats.TryGetValue(attacker.UserIDString, out stats))
                stats.tcsdestroyed++;
        }
        void OnEntityDeath(NPCPlayer scientist, HitInfo info)
        {
            BasePlayer attacker = info?.InitiatorPlayer;
            if (attacker == null) return;

            if (playerStats.TryGetValue(attacker.UserIDString, out stats))
                stats.npcStats.scientists++;
        }
        void OnEntityDeath(BaseAnimalNPC animal, HitInfo info)
        {
            BasePlayer attacker = info?.InitiatorPlayer;
            if (attacker == null) return;

            if (playerStats.TryGetValue(attacker.UserIDString, out stats))
            {
                switch (animal.ShortPrefabName)
                {
                    case "chicken":
                        stats.npcStats.chickens++;
                        break;
                    case "boar":
                        stats.npcStats.boars++;
                        break;
                    case "stag":
                        stats.npcStats.deers++;
                        break;
                    case "horse":
                        stats.npcStats.horses++;
                        break;
                    case "wolf":
                        stats.npcStats.wolves++;
                        break;
                    case "polarbear":
                    case "bear":
                        stats.npcStats.bears++;
                        break;
                }
            }
        }
        void OnEntityTakeDamage(BaseHelicopter entity, HitInfo info)
        {
            if (info.Initiator is BasePlayer)
                HeliAttacker = info?.InitiatorPlayer;
        }
        void OnEntityDeath(BaseHelicopter heli, HitInfo info)
        {
            if (HeliAttacker == null) return;
            if (playerStats.TryGetValue(HeliAttacker.UserIDString, out stats))
                stats.npcStats.helicopters++;

            HeliAttacker = null;
        }
        void OnEntityDeath(BradleyAPC bradley, HitInfo info)
        {
            BasePlayer attacker = info?.InitiatorPlayer;
            if (attacker == null) return;

            if (playerStats.TryGetValue(attacker.UserIDString, out stats))
                stats.npcStats.bradleys++;
        }
        void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (!playerStats.TryGetValue(player.UserIDString, out stats)) return;

            switch (player.GetActiveItem()?.info.shortname)
            {
                case "explosive.timed":
                    stats.c4thrown++;
                    break;

                case "explosive.satchel":
                    stats.satchelsthrown++;
                    break;
            }
        }
        void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            if (!playerStats.TryGetValue(player.UserIDString, out stats)) return;
            stats.rocketsfired++;
        }
        object OnPlayerWound(BasePlayer player, HitInfo hitInfo)
        {
            if (playerStats.TryGetValue(player.UserIDString, out stats))
                stats.wounded++;

            return null;
        }
        object OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            BasePlayer victim = info.HitEntity?.ToPlayer();
            if (victim != null)
            {
                if (!playerStats.TryGetValue(attacker.UserIDString, out stats)) return null;
                switch (info.boneName.ToLower())
                {
                    case "head":
                        stats.hits.head++;
                        break;

                    case "neck":
                    case "chest":
                    case "lowerspine":
                        stats.hits.torso++;
                        break;

                    case "pelvis":
                    case "hip":
                        stats.hits.leftleg++;
                        stats.hits.rightleg++;
                        break;

                    case "left knee":
                        stats.hits.leftleg++;
                        break;

                    case "right knee":
                        stats.hits.rightleg++;
                        break;

                    case "left foot":
                        stats.hits.leftfoot++;
                        break;

                    case "right foot":
                        stats.hits.rightfoot++;
                        break;

                    case "left arm":
                    case "left forearm":
                    case "left hand":
                        stats.hits.lefthand++;
                        break;

                    case "right arm":
                    case "right forearm":
                    case "right hand":
                        stats.hits.righthand++;
                        break;
                }
            }

            return null;
        }
        void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        {
            if (!playerStats.TryGetValue(player.UserIDString, out stats)) return;
            stats.bfired++;
        }
        #endregion

        #region Functions
        public string RemoveSpecialCharacters(string str)
        {
            return str.Replace("'", "").Replace("@", "");
        }
        string CalculateAccuracy(int bulletsFired, BodyParts hits)
        {
            int totalHits = hits.head + hits.torso + hits.lefthand + hits.righthand + hits.leftleg + hits.rightleg + hits.leftfoot + hits.rightfoot;
            if (totalHits == 0 || bulletsFired == 0) return "100%";
            return $"{Math.Round(((float)totalHits / bulletsFired) * 100, 2)}%";
        }
        void CreateTable(string tableName)
        {
            Sql query = Sql.Builder.Append(TableExists, tableName);
            SQL.Query(query, connection, list =>
            {
                if (list.Count == 0)
                {
                    var queryNew = Sql.Builder.Append($"CALL `createTable`('{tableName}')");
                    SQL.Query(queryNew, connection, created => { });
                }
            });
        }
        void SaveAllStats()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                SavePlayerStats(player);
        }
        void SavePlayerStats(BasePlayer player, bool disconnect = false)
        {
            if (connection == null || connection.Con == null) return;
            if (player == null) return;
            if (playerStats.TryGetValue(player.UserIDString, out stats))
            {
                stats.playtime += (int)DateTime.Now.Subtract(stats.connected).TotalMinutes;
                stats.connected = DateTime.Now;

                var query = Sql.Builder.Append($"UPDATE {config.serverPrefix}_stats_wipe SET name = '{RemoveSpecialCharacters(player.displayName)}', connections = {stats.connections}, playtime = {stats.playtime}, kills = {stats.kills}, deaths = {stats.deaths}, bfired = {stats.bfired}, suicides = {stats.suicides}, wounded = {stats.wounded}, c4thrown = {stats.c4thrown}, satchelsthrown = {stats.satchelsthrown}, rocketsfired = {stats.rocketsfired}, tcsdestroyed = {stats.tcsdestroyed}, chickens = {stats.npcStats.chickens}, boars = {stats.npcStats.boars}, deers = {stats.npcStats.deers}, horses = {stats.npcStats.horses}, wolves = {stats.npcStats.wolves}, bears = {stats.npcStats.bears}, scientists = {stats.npcStats.scientists}, helicopters = {stats.npcStats.helicopters}, bradleys = {stats.npcStats.bradleys}, ak47 = {stats.weaponStats.ak47}, lr300 = {stats.weaponStats.lr300}, m39 = {stats.weaponStats.m39}, sar = {stats.weaponStats.sar}, m249 = {stats.weaponStats.m249}, hmlmg = {stats.weaponStats.hmlmg}, l96 = {stats.weaponStats.l96}, bolt = {stats.weaponStats.bolt}, mp5 = {stats.weaponStats.mp5}, thompson = {stats.weaponStats.thompson}, custom = {stats.weaponStats.custom}, pump = {stats.weaponStats.pump}, doublebarrel = {stats.weaponStats.doublebarrel}, spaz12 = {stats.weaponStats.spaz12}, m92 = {stats.weaponStats.m92}, python = {stats.weaponStats.python}, prototype17 = {stats.weaponStats.prototype}, semipistol = {stats.weaponStats.semipistol}, revolver = {stats.weaponStats.revolver}, nailgun = {stats.weaponStats.nailgun}, waterpipe = {stats.weaponStats.waterpipe}, eoka = {stats.weaponStats.eoka}, compound = {stats.weaponStats.compound}, crossbow = {stats.weaponStats.crossbow}, bow = {stats.weaponStats.bow}, head_hits = {stats.hits.head}, torso_hits = {stats.hits.torso}, leftarm_hits = {stats.hits.lefthand}, rightarm_hits = {stats.hits.righthand}, leftleg_hits = {stats.hits.leftleg}, rightleg_hits = {stats.hits.rightleg}, leftfoot_hits = {stats.hits.leftfoot}, rightfoot_hits = {stats.hits.rightfoot} WHERE steamid = {player.UserIDString}");
                SQL.Update(query, connection, output =>
                {
                    if (player == null) return;
                    if (config.developerMode) Puts(GetLang("UpdatedPlayerStats", RemoveSpecialCharacters(player.displayName)));
                });
            }
        }
        IEnumerator Startup()
        {
            CreateTable($"{config.serverPrefix}_stats_wipe");
            CreateTable($"{config.serverPrefix}_stats_overall");
            yield return CoroutineEx.waitForSeconds(3f);

            Sql query = Sql.Builder.Append($"SELECT * FROM {config.serverPrefix}_stats_wipe");
            SQL.Query(query, connection, list =>
            {
                playerStats.Clear();
                foreach (var stats in list)
                {
                    if (playerStats.ContainsKey(stats.Values.ElementAt(2).ToString())) continue;
                    playerStats.Add(stats.Values.ElementAt(2).ToString(), new PlayerStats
                    {
                        name = stats.Values.ElementAt(1).ToString(),
                        connections = Convert.ToInt32(stats.Values.ElementAt(3)),
                        playtime = Convert.ToInt32(stats.Values.ElementAt(4)),
                        kills = Convert.ToInt32(stats.Values.ElementAt(5)),
                        deaths = Convert.ToInt32(stats.Values.ElementAt(6)),
                        bfired = Convert.ToInt32(stats.Values.ElementAt(7)),
                        suicides = Convert.ToInt32(stats.Values.ElementAt(8)),
                        wounded = Convert.ToInt32(stats.Values.ElementAt(9)),
                        c4thrown = Convert.ToInt32(stats.Values.ElementAt(10)),
                        satchelsthrown = Convert.ToInt32(stats.Values.ElementAt(11)),
                        rocketsfired = Convert.ToInt32(stats.Values.ElementAt(12)),
                        tcsdestroyed = Convert.ToInt32(stats.Values.ElementAt(13)),
                        npcStats = new NPCStats
                        {
                            chickens = Convert.ToInt32(stats.Values.ElementAt(14)),
                            boars = Convert.ToInt32(stats.Values.ElementAt(15)),
                            deers = Convert.ToInt32(stats.Values.ElementAt(16)),
                            horses = Convert.ToInt32(stats.Values.ElementAt(17)),
                            wolves = Convert.ToInt32(stats.Values.ElementAt(18)),
                            bears = Convert.ToInt32(stats.Values.ElementAt(19)),
                            scientists = Convert.ToInt32(stats.Values.ElementAt(20)),
                            helicopters = Convert.ToInt32(stats.Values.ElementAt(21)),
                            bradleys = Convert.ToInt32(stats.Values.ElementAt(22)),
                        },
                        weaponStats = new WeaponStats
                        {
                            ak47 = Convert.ToInt32(stats.Values.ElementAt(23)),
                            lr300 = Convert.ToInt32(stats.Values.ElementAt(24)),
                            m39 = Convert.ToInt32(stats.Values.ElementAt(25)),
                            sar = Convert.ToInt32(stats.Values.ElementAt(26)),
                            m249 = Convert.ToInt32(stats.Values.ElementAt(27)),
                            hmlmg = Convert.ToInt32(stats.Values.ElementAt(28)),
                            l96 = Convert.ToInt32(stats.Values.ElementAt(29)),
                            bolt = Convert.ToInt32(stats.Values.ElementAt(30)),
                            mp5 = Convert.ToInt32(stats.Values.ElementAt(31)),
                            thompson = Convert.ToInt32(stats.Values.ElementAt(32)),
                            custom = Convert.ToInt32(stats.Values.ElementAt(33)),
                            pump = Convert.ToInt32(stats.Values.ElementAt(34)),
                            doublebarrel = Convert.ToInt32(stats.Values.ElementAt(35)),
                            spaz12 = Convert.ToInt32(stats.Values.ElementAt(36)),
                            m92 = Convert.ToInt32(stats.Values.ElementAt(37)),
                            python = Convert.ToInt32(stats.Values.ElementAt(38)),
                            prototype = Convert.ToInt32(stats.Values.ElementAt(54)),
                            semipistol = Convert.ToInt32(stats.Values.ElementAt(39)),
                            revolver = Convert.ToInt32(stats.Values.ElementAt(40)),
                            nailgun = Convert.ToInt32(stats.Values.ElementAt(55)),
                            waterpipe = Convert.ToInt32(stats.Values.ElementAt(41)),
                            eoka = Convert.ToInt32(stats.Values.ElementAt(42)),
                            compound = Convert.ToInt32(stats.Values.ElementAt(43)),
                            crossbow = Convert.ToInt32(stats.Values.ElementAt(44)),
                            bow = Convert.ToInt32(stats.Values.ElementAt(45)),
                        },
                        hits = new BodyParts
                        {
                            head = Convert.ToInt32(stats.Values.ElementAt(46)),
                            torso = Convert.ToInt32(stats.Values.ElementAt(47)),
                            lefthand = Convert.ToInt32(stats.Values.ElementAt(48)),
                            righthand = Convert.ToInt32(stats.Values.ElementAt(49)),
                            leftleg = Convert.ToInt32(stats.Values.ElementAt(50)),
                            rightleg = Convert.ToInt32(stats.Values.ElementAt(51)),
                            leftfoot = Convert.ToInt32(stats.Values.ElementAt(52)),
                            rightfoot = Convert.ToInt32(stats.Values.ElementAt(53)),
                        },
                    });
                };


                Sql overallquery = Sql.Builder.Append($"SELECT * FROM {config.serverPrefix}_stats_overall");
                SQL.Query(overallquery, connection, newlist =>
                {
                    playerStatsOverall.Clear();
                    foreach (var stats in newlist)
                    {
                        if (playerStatsOverall.ContainsKey(stats.Values.ElementAt(2).ToString())) continue;
                        playerStatsOverall.Add(stats.Values.ElementAt(2).ToString(), new PlayerStats
                        {
                            name = stats.Values.ElementAt(1).ToString(),
                            connections = Convert.ToInt32(stats.Values.ElementAt(3)),
                            playtime = Convert.ToInt32(stats.Values.ElementAt(4)),
                            kills = Convert.ToInt32(stats.Values.ElementAt(5)),
                            deaths = Convert.ToInt32(stats.Values.ElementAt(6)),
                            bfired = Convert.ToInt32(stats.Values.ElementAt(7)),
                            suicides = Convert.ToInt32(stats.Values.ElementAt(8)),
                            wounded = Convert.ToInt32(stats.Values.ElementAt(9)),
                            c4thrown = Convert.ToInt32(stats.Values.ElementAt(10)),
                            satchelsthrown = Convert.ToInt32(stats.Values.ElementAt(11)),
                            rocketsfired = Convert.ToInt32(stats.Values.ElementAt(12)),
                            tcsdestroyed = Convert.ToInt32(stats.Values.ElementAt(13)),
                            npcStats = new NPCStats
                            {
                                chickens = Convert.ToInt32(stats.Values.ElementAt(14)),
                                boars = Convert.ToInt32(stats.Values.ElementAt(15)),
                                deers = Convert.ToInt32(stats.Values.ElementAt(16)),
                                horses = Convert.ToInt32(stats.Values.ElementAt(17)),
                                wolves = Convert.ToInt32(stats.Values.ElementAt(18)),
                                bears = Convert.ToInt32(stats.Values.ElementAt(19)),
                                scientists = Convert.ToInt32(stats.Values.ElementAt(20)),
                                helicopters = Convert.ToInt32(stats.Values.ElementAt(21)),
                                bradleys = Convert.ToInt32(stats.Values.ElementAt(22)),
                            },
                            weaponStats = new WeaponStats
                            {
                                ak47 = Convert.ToInt32(stats.Values.ElementAt(23)),
                                lr300 = Convert.ToInt32(stats.Values.ElementAt(24)),
                                m39 = Convert.ToInt32(stats.Values.ElementAt(25)),
                                sar = Convert.ToInt32(stats.Values.ElementAt(26)),
                                m249 = Convert.ToInt32(stats.Values.ElementAt(27)),
                                hmlmg = Convert.ToInt32(stats.Values.ElementAt(28)),
                                l96 = Convert.ToInt32(stats.Values.ElementAt(29)),
                                bolt = Convert.ToInt32(stats.Values.ElementAt(30)),
                                mp5 = Convert.ToInt32(stats.Values.ElementAt(31)),
                                thompson = Convert.ToInt32(stats.Values.ElementAt(32)),
                                custom = Convert.ToInt32(stats.Values.ElementAt(33)),
                                pump = Convert.ToInt32(stats.Values.ElementAt(34)),
                                doublebarrel = Convert.ToInt32(stats.Values.ElementAt(35)),
                                spaz12 = Convert.ToInt32(stats.Values.ElementAt(36)),
                                m92 = Convert.ToInt32(stats.Values.ElementAt(37)),
                                python = Convert.ToInt32(stats.Values.ElementAt(38)),
                                prototype = Convert.ToInt32(stats.Values.ElementAt(54)),
                                semipistol = Convert.ToInt32(stats.Values.ElementAt(39)),
                                revolver = Convert.ToInt32(stats.Values.ElementAt(40)),
                                nailgun = Convert.ToInt32(stats.Values.ElementAt(55)),
                                waterpipe = Convert.ToInt32(stats.Values.ElementAt(41)),
                                eoka = Convert.ToInt32(stats.Values.ElementAt(42)),
                                compound = Convert.ToInt32(stats.Values.ElementAt(43)),
                                crossbow = Convert.ToInt32(stats.Values.ElementAt(44)),
                                bow = Convert.ToInt32(stats.Values.ElementAt(45)),
                            },
                            hits = new BodyParts
                            {
                                head = Convert.ToInt32(stats.Values.ElementAt(46)),
                                torso = Convert.ToInt32(stats.Values.ElementAt(47)),
                                lefthand = Convert.ToInt32(stats.Values.ElementAt(48)),
                                righthand = Convert.ToInt32(stats.Values.ElementAt(49)),
                                leftleg = Convert.ToInt32(stats.Values.ElementAt(50)),
                                rightleg = Convert.ToInt32(stats.Values.ElementAt(51)),
                                leftfoot = Convert.ToInt32(stats.Values.ElementAt(52)),
                                rightfoot = Convert.ToInt32(stats.Values.ElementAt(53)),
                            },
                        });
                    }
                });
            });

            yield return null;
        }
        #endregion

        #region Commands
        [Command("stats.save")]
        private void SaveStatsCMD(IPlayer iPlayer, string command, string[] args)
        {
            if (!iPlayer.IsServer) return;
            if (args.Length > 0)
            {
                BasePlayer player = BasePlayer.FindAwakeOrSleeping(args[0]);
                if (player == null) return;
                SavePlayerStats(player);
            }
            else
                SaveAllStats();
        }

        [Command("stats")]
        private void MyStatsCMD(IPlayer iPlayer, string command, string[] args)
        {
            if (iPlayer.IsServer || !config.useIngameUI) return;
            BasePlayer player = iPlayer.Object as BasePlayer;

            DestroyLeaderboard(player);
            CuiHelper.DestroyUi(player, "BetterStatsContainer");

            if (args.Length == 1)
                BetterStatsContainer(player, args[0]);
            else
                BetterStatsContainer(player, player.UserIDString);
        }


        [Command("leaderboard")]
        private void LeaderboardCMD(IPlayer iPlayer, string command, string[] args)
        {
            if (iPlayer.IsServer) return;
            BasePlayer player = iPlayer.Object as BasePlayer;

            BetterStatsLeaderboard(player);
        }

        [Command("destroyui")]
        private void DestroyUICMD(IPlayer iPlayer, string command, string[] args)
        {
            if (iPlayer.IsServer) return;
            BasePlayer player = iPlayer.Object as BasePlayer;
            CuiHelper.DestroyUi(player, args[0]);
        }

        [Command("destroyleaderboard")]
        private void DestroyLeaderboardCMD(IPlayer iPlayer, string command, string[] args)
        {
            if (iPlayer.IsServer) return;
            BasePlayer player = iPlayer.Object as BasePlayer;
            DestroyLeaderboard(player);
        }

        void DestroyLeaderboard(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "BetterStatsLeaderboard");
            CuiHelper.DestroyUi(player, "HeaderPanel");
            CuiHelper.DestroyUi(player, "PlayersPanel");
        }

        [Command("stats.action")]
        private void AtatsActionCMD(IPlayer iPlayer, string command, string[] args)
        {
            if (iPlayer.IsServer) return;
            BasePlayer player = iPlayer.Object as BasePlayer;
            if (args.Length == 0) return;

            switch (args[0])
            {
                case "page":
                    if (args.Length != 4) return;

                    bool wipe = Convert.ToBoolean(args[2]);
                    int page = Convert.ToInt32(args[3]);
                    switch (args[1])
                    {
                        case "next":
                            HeaderPanel(player, wipe, page + 1);
                            break;

                        case "prev":
                            HeaderPanel(player, wipe, page - 1);
                            break;
                    }
                    break;

                case "stats":
                    if (args.Length != 2) return;
                    switch (args[1])
                    {
                        case "wipe":
                            HeaderPanel(player);
                            break;

                        case "overall":
                            HeaderPanel(player, false, 1);
                            break;
                    }
                    break;
            }
        }

        [Command("reloadstats")]
        private void ReloadStatsCMD(IPlayer iPlayer, string command, string[] args)
        {
            if (iPlayer.IsServer) return;
            BasePlayer player = iPlayer.Object as BasePlayer;
            if (args.Length == 0) return;
            BetterStatsContainer(player, args[0], args[1] == "next" ? 2 : 1, Convert.ToBoolean(args[2]));
        }
        #endregion

        #region UI
        private void BetterStatsContainer(BasePlayer player, string userId, int page = 1, bool wipeTable = true)
        {
            CuiHelper.DestroyUi(player, "BetterStatsContainer");

            if (wipeTable)
            {
                if (!playerStats.TryGetValue(userId, out stats))
                {
                    player.ChatMessage(GetLang("PlayerNotFound"));
                    return;
                }
            }
            else
            {
                if (!playerStatsOverall.TryGetValue(userId, out stats))
                {
                    player.ChatMessage(GetLang("PlayerNotFoundOverall"));
                    return;
                }
            }

            string playtime = "0";
            TimeSpan elapsedTime = new TimeSpan(0, stats.playtime, 0);
            int day = elapsedTime.Days;
            int hour = elapsedTime.Hours;
            int mins = elapsedTime.Minutes;

            if (day > 0)
                playtime = $"{day}d {hour}h {mins}m";
            else if (hour > 0)
                playtime = $"{hour}h {mins}m";
            else if (mins > 0)
                playtime = $"{mins}m";

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.10 0.10 0.08 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-630.78 -359.31", OffsetMax = "640.78 355.98" }
            }, "Overlay", "BetterStatsContainer");

            container.Add(new CuiElement
            {
                Name = "Grunge",
                Parent = "BetterStatsContainer",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 0.08", Png = ImageLibrary?.Call<string>("GetImage", "GrungeBackground"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.08 0.07 0.07 0.8" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-635.79 -69", OffsetMax = "635.80 0" }
            }, "BetterStatsContainer", "BetterStatsHeader");

            container.Add(new CuiElement
            {
                Name = "HeaderTitle",
                Parent = "BetterStatsHeader",
                Components = {
                    new CuiTextComponent { Text = GetLang("UITitle"), Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "32.4 -18.17", OffsetMax = "211.82 18.17" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = $"{(wipeTable ? "0.23 0.29 0.15 1" : "0.58 0.18 0.12 1")}", Command = $"reloadstats {userId} prev true" },
                Text = { Text = GetLang("UIWipeButton"), Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = $"{(wipeTable ? "0.65 0.80 0.38 1" : "0.78 0.59 0.59 1")}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-140.31 -16.65", OffsetMax = "-12.49 16.65" }
            }, "BetterStatsHeader", "WipeButton");

            container.Add(new CuiButton
            {
                Button = { Color = $"{(!wipeTable ? "0.23 0.29 0.15 1" : "0.58 0.18 0.12 1")}", Command = $"reloadstats {userId} prev false" },
                Text = { Text = GetLang("UIOverallButton"), Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = $"{(!wipeTable ? "0.65 0.80 0.38 1" : "0.78 0.59 0.59 1")}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.89 -16.65", OffsetMax = "136.71 16.65" }
            }, "BetterStatsHeader", "OverallButton");

            container.Add(new CuiButton
            {
                Button = { Color = "0.58 0.18 0.12 1", Command = "destroyui BetterStatsContainer" },
                RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-141.22 -13.03", OffsetMax = "-31.97 16.65" }
            }, "BetterStatsHeader", "CloseButton");

            container.Add(new CuiElement
            {
                Name = "CloseText",
                Parent = "CloseButton",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIExitButton"), Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "0.78 0.59 0.59 1" },
                    new CuiRectTransformComponent { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-63.06 -15", OffsetMax = "0 15" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Image_9016",
                Parent = "CloseButton",
                Components = {
                    new CuiTextComponent { Text = "X", Font = "robotocondensed-bold.ttf", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "0.78 0.59 0.59 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "24.16 -7.36", OffsetMax = "38.9 7.36" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.08 0.07 0.07 0.80" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-615.24 -295.74", OffsetMax = "-313.75 271.71" }
            }, "BetterStatsContainer", "PlayerViewPanel");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.08 0.07 0.07 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-150.74 -37", OffsetMax = "150.74 0" }
            }, "PlayerViewPanel", "PlayerViewHeader");

            container.Add(new CuiElement
            {
                Name = "PlayerViewHeaderText",
                Parent = "PlayerViewHeader",
                Components = {
                    new CuiTextComponent { Text = $"{stats.name}", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-139.06 -15.21", OffsetMax = "150.74 15.21" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "PlayerImage",
                Parent = "PlayerViewPanel",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "PlayerImage"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-90.28 -269.7", OffsetMax = "90.28 229.7" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.11 0.25 0.37 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "36.93 191.3", OffsetMax = "141.06 217.1" }
            }, "PlayerViewPanel", "HeadContainer");

            container.Add(new CuiElement
            {
                Name = "HeadText",
                Parent = "HeadContainer",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIHead"), Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.28 0.60 0.83 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-49.18 -8.49", OffsetMax = "-12.81 8.60" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.15 0.33 0.47 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-3.02 -8.54", OffsetMax = "49.22 8.60" }
            }, "HeadContainer", "HitsContainer");

            container.Add(new CuiElement
            {
                Name = "HeadHits",
                Parent = "HitsContainer",
                Components = {
                    new CuiTextComponent { Text = $"{stats.hits.head} Hits", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.28 0.60 0.83 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-26.66 -8.57", OffsetMax = "26.12 8.75" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.11 0.25 0.37 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-52.06 73.1", OffsetMax = "52.06 98.9" }
            }, "PlayerViewPanel", "TorsoContainer");

            container.Add(new CuiElement
            {
                Name = "TorsoText",
                Parent = "TorsoContainer",
                Components = {
                    new CuiTextComponent { Text = GetLang("UITorso"), Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.28 0.60 0.83 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-49.18 -8.49", OffsetMax = "-12.81 8.60" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.15 0.33 0.47 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-3.02 -8.54", OffsetMax = "49.22 8.60" }
            }, "TorsoContainer", "HitsContainer");

            container.Add(new CuiElement
            {
                Name = "TorsoHits",
                Parent = "HitsContainer",
                Components = {
                    new CuiTextComponent { Text = $"{stats.hits.torso} Hits", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.28 0.60 0.83 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-26.66 -8.57", OffsetMax = "26.12 8.75" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.11 0.25 0.37 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-135.56 -143.5", OffsetMax = "-31.43 -117.7" }
            }, "PlayerViewPanel", "LeftLegContainer");

            container.Add(new CuiElement
            {
                Name = "LeftLegText",
                Parent = "LeftLegContainer",
                Components = {
                    new CuiTextComponent { Text = GetLang("UILeftLeg"), Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "0.28 0.60 0.83 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-47.18 -8.49", OffsetMax = "-10.81 8.60" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.15 0.33 0.47 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-3.02 -8.54", OffsetMax = "49.22 8.60" }
            }, "LeftLegContainer", "HitsContainer");

            container.Add(new CuiElement
            {
                Name = "LeftLegHits",
                Parent = "HitsContainer",
                Components = {
                    new CuiTextComponent { Text = $"{stats.hits.leftleg} Hits", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.28 0.60 0.83 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-26.12 -8.57", OffsetMax = "26.12 8.75" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.11 0.25 0.37 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "30.63 -143.5", OffsetMax = "134.76 -117.7" }
            }, "PlayerViewPanel", "RightLegContainer");

            container.Add(new CuiElement
            {
                Name = "RightLegText",
                Parent = "RightLegContainer",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIRightLeg"), Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "0.28 0.60 0.83 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "9.48 -8.49", OffsetMax = "48.74 8.60" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.15 0.33 0.47 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-47.82 -8.49", OffsetMax = "4.42 8.65" }
            }, "RightLegContainer", "HitsContainer");

            container.Add(new CuiElement
            {
                Name = "RightLegHits",
                Parent = "HitsContainer",
                Components = {
                    new CuiTextComponent { Text = $"{stats.hits.rightleg} Hits", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.28 0.60 0.83 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-26.12 -8.57", OffsetMax = "26.12 8.75" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.11 0.25 0.37 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-141.96 -269.7", OffsetMax = "-37.83 -243.9" }
            }, "PlayerViewPanel", "LeftFootContainer");

            container.Add(new CuiElement
            {
                Name = "LeftFootText",
                Parent = "LeftFootContainer",
                Components = {
                    new CuiTextComponent { Text = GetLang("UILeftFoot"), Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "0.28 0.60 0.83 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-47.18 -8.49", OffsetMax = "-10.81 8.60" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.15 0.33 0.47 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-3.02 -8.54", OffsetMax = "49.22 8.60" }
            }, "LeftFootContainer", "HitsContainer");

            container.Add(new CuiElement
            {
                Name = "LeftFootHits",
                Parent = "HitsContainer",
                Components = {
                    new CuiTextComponent { Text = $"{stats.hits.leftfoot} Hits", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.28 0.60 0.83 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-26.12 -8.57", OffsetMax = "26.12 8.75" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.11 0.25 0.37 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "33.73 -256.3", OffsetMax = "137.86 -230.5" }
            }, "PlayerViewPanel", "RightFootContainer");

            container.Add(new CuiElement
            {
                Name = "RightFootText",
                Parent = "RightFootContainer",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIRightFoot"), Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "0.28 0.60 0.83 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "6.06 -8.49", OffsetMax = "48.74 8.60" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.15 0.33 0.47 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-48.92 -8.49", OffsetMax = "3.32 8.65" }
            }, "RightFootContainer", "HitsContainer");

            container.Add(new CuiElement
            {
                Name = "RightFootHits",
                Parent = "HitsContainer",
                Components = {
                    new CuiTextComponent { Text = $"{stats.hits.rightfoot} Hits", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.28 0.60 0.83 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-26.12 -8.57", OffsetMax = "26.12 8.75" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.11 0.25 0.37 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "40.73 12.1", OffsetMax = "144.86 37.9" }
            }, "PlayerViewPanel", "RightArmContainer");

            container.Add(new CuiElement
            {
                Name = "RightArmText",
                Parent = "RightArmContainer",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIRightArm"), Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "0.28 0.60 0.83 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "6.89 -8.43", OffsetMax = "48.90 8.65" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.15 0.33 0.47 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-47.82 -8.49", OffsetMax = "4.42 8.65" }
            }, "RightArmContainer", "HitsContainer");

            container.Add(new CuiElement
            {
                Name = "RightArmHits",
                Parent = "HitsContainer",
                Components = {
                    new CuiTextComponent { Text = $"{stats.hits.righthand} Hits", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.28 0.60 0.83 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-26.12 -8.57", OffsetMax = "26.12 8.75" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.11 0.25 0.37 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-141.96 12.1", OffsetMax = "-37.83 37.9" }
            }, "PlayerViewPanel", "LeftArmContainer");

            container.Add(new CuiElement
            {
                Name = "LeftArmText",
                Parent = "LeftArmContainer",
                Components = {
                    new CuiTextComponent { Text = GetLang("UILeftArm"), Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "0.28 0.60 0.83 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-47.18 -8.49", OffsetMax = "-10.81 8.60" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.15 0.33 0.47 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-3.02 -8.54", OffsetMax = "49.22 8.60" }
            }, "LeftArmContainer", "HitsContainer");

            container.Add(new CuiElement
            {
                Name = "LeftArmHits",
                Parent = "HitsContainer",
                Components = {
                    new CuiTextComponent { Text = $"{stats.hits.lefthand} Hits", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.28 0.60 0.83 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-26.12 -8.57", OffsetMax = "26.12 8.75" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.08 0.07 0.07 0.80" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-300.37 -173.89", OffsetMax = "-14.22 271.71" }
            }, "BetterStatsContainer", "PlayerStatsPanel");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.08 0.07 0.07 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-143.07 -30.14", OffsetMax = "143.07 0.28" }
            }, "PlayerStatsPanel", "HeaderPanel");

            container.Add(new CuiElement
            {
                Name = "PlayerStatsText",
                Parent = "HeaderPanel",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIPlayerStats"), Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-130.85 -15.21", OffsetMax = "58.87 15.21" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-126.61 150.99", OffsetMax = "-12.23 177.09" }
            }, "PlayerStatsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIConnects"), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-24.63 -11.45", OffsetMax = "54.03 11.45" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "ConnectionsIcon"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-44.1 -8.2", OffsetMax = "-30.09 5.8" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-126.61 124.88", OffsetMax = "-12.23 150.98" }
            }, "PlayerStatsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = $"{stats.connections}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.04 150.99", OffsetMax = "122.43 177.09" }
            }, "PlayerStatsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIPlaytime"), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-22.91 -13.05", OffsetMax = "54.51 13.05" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "PlaytimeIcon"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-33.07 -6.84", OffsetMax = "-19.38 6.84" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.04 124.88", OffsetMax = "122.43 150.98" }
            }, "PlayerStatsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = playtime, Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-126.48 87.05", OffsetMax = "-12.10 113.15" }
            }, "PlayerStatsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIKills"), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-33.19 -13.05", OffsetMax = "48.59 13.05" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Sprite = "assets/icons/bullet.png", Material = "assets/icons/iconmaterial.mat" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-29.78 -8.08", OffsetMax = "-13.61 8.08" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-126.48 60.94", OffsetMax = "-12.10 87.05" }
            }, "PlayerStatsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = $"{stats.kills}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.17 87.05", OffsetMax = "122.56 113.15" }
            }, "PlayerStatsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIDeaths"), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-33.35 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "DeathsIcon"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-33.35 -9.17", OffsetMax = "-18.19 5.97" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.18 60.94", OffsetMax = "122.56 87.05" }
            }, "PlayerStatsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = $"{stats.deaths}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-126.09 22.93", OffsetMax = "-11.70 49.04" }
            }, "PlayerStatsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIWounded"), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32.35 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "WoundedIcon"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-38.79 -8.09", OffsetMax = "-25.40 5.29" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-126.09 -3.16", OffsetMax = "-11.70 22.93" }
            }, "PlayerStatsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = $"{stats.wounded}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.57 22.93", OffsetMax = "122.96 49.04" }
            }, "PlayerStatsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UISuicides"), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-22.79 -13.05", OffsetMax = "53.79 13.05" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "SuicidesIcon"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31.91 -8.51", OffsetMax = "-17.89 5.51" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.57 -3.16", OffsetMax = "122.96 22.93" }
            }, "PlayerStatsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = $"{stats.suicides}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-126.09 -41.39", OffsetMax = "-11.70 -15.29" }
            }, "PlayerStatsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIKDRatio"), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-23.85 -13.05", OffsetMax = "50.63 13.05" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "KDRIcon"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-37.34 -6.74", OffsetMax = "-23.85 6.74" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-126.09 -67.49", OffsetMax = "-11.70 -41.39" }
            }, "PlayerStatsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = string.Format("{0}", stats.deaths == 0 ? stats.kills : (float)Math.Round(((float)stats.kills) / stats.deaths, 2)), Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.57 -41.39", OffsetMax = "122.96 -15.29" }
            }, "PlayerStatsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIAccuracy"), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-16.99 -13.05", OffsetMax = "54.59 13.05" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "AccuracyIcon"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.92 -6.96", OffsetMax = "-16.99 6.96" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.57 -67.49", OffsetMax = "122.96 -41.39" }
            }, "PlayerStatsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = CalculateAccuracy(stats.bfired, stats.hits), Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-125.69 -108.02", OffsetMax = "-11.30 -81.92" }
            }, "PlayerStatsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UISatchels"), Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-35.91 -13.05", OffsetMax = "44.31 13.05" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "SatchelsIcon"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-40.9 -8.13", OffsetMax = "-27.02 5.73" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-125.69 -134.12", OffsetMax = "-11.30 -108.02" }
            }, "PlayerStatsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = $"{stats.satchelsthrown}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.97 -108.02", OffsetMax = "123.35 -81.92" }
            }, "PlayerStatsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIC4"), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "C4Icon"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25.02 -8.62", OffsetMax = "-10.57 5.82" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.97 -134.12", OffsetMax = "123.35 -108.02" }
            }, "PlayerStatsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = $"{stats.c4thrown}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-125.42 -173.77", OffsetMax = "-11.04 -147.66" }
            }, "PlayerStatsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIRockets"), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-24.04 -13.05", OffsetMax = "50.44 13.05" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "RocketsIcon"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-35.3 -7.99", OffsetMax = "-20.90 6.39" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-125.42 -199.87", OffsetMax = "-11.04 -173.77" }
            }, "PlayerStatsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = $"{stats.rocketsfired}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "9.23 -173.77", OffsetMax = "123.62 -147.66" }
            }, "PlayerStatsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UITCsDestroyed"), Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32.88 -13.05", OffsetMax = "54.51 13.05" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "TCIcon"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-46.71 -6.91", OffsetMax = "-32.88 6.91" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "9.23 -199.87", OffsetMax = "123.62 -173.77" }
            }, "PlayerStatsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = $"{stats.tcsdestroyed}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });



            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.08 0.07 0.07 0.80" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-0.00 -107.41", OffsetMax = "286.14 271.71" }
            }, "BetterStatsContainer", "NPCKillsPanel");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.08 0.07 0.07 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-143.07 -30.14", OffsetMax = "143.07 0.28" }
            }, "NPCKillsPanel", "HeaderPanel");

            container.Add(new CuiElement
            {
                Name = "NPCStatsText",
                Parent = "HeaderPanel",
                Components = {
                    new CuiTextComponent { Text = GetLang("UINPCKills"), Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-130.85 -15.21", OffsetMax = "58.87 15.21" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-126.61 117.75", OffsetMax = "-12.23 143.85" }
            }, "NPCKillsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIChickens"), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-21.46 -11.45", OffsetMax = "57.19 11.45" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "ChickenIcon"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-39 -8.2", OffsetMax = "-25 5.8" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-126.61 91.64", OffsetMax = "-12.23 117.74" }
            }, "NPCKillsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = $"{stats.npcStats.chickens}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.04 117.75", OffsetMax = "122.43 143.85" }
            }, "NPCKillsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIBoars"), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-24.81 -13.05", OffsetMax = "52.61 13.05" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "BoarIcon"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-24.81 -8.24", OffsetMax = "-11.12 5.44" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.04 91.64", OffsetMax = "122.43 117.74" }
            }, "NPCKillsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = $"{stats.npcStats.boars}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-126.48 53.81", OffsetMax = "-12.10 79.91" }
            }, "NPCKillsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIStags"), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-27.88 -13.05", OffsetMax = "53.91 13.05" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "DeerIcon"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-26.68 -9.18", OffsetMax = "-10.51 6.98" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-126.48 27.70", OffsetMax = "-12.10 53.80" }
            }, "NPCKillsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = $"{stats.npcStats.deers}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.17 53.81", OffsetMax = "122.56 79.91" }
            }, "NPCKillsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIHorses"), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-33.35 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "HorseIcon"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31.77 -9.17", OffsetMax = "-16.62 5.97" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.18 27.70", OffsetMax = "122.56 53.80" }
            }, "NPCKillsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = $"{stats.npcStats.horses}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-126.09 -10.30", OffsetMax = "-11.70 15.80" }
            }, "NPCKillsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIWolves"), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32.35 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "WolveIcon"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.59 -7.49", OffsetMax = "-17.20 5.89" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-126.09 -36.40", OffsetMax = "-11.70 -10.30" }
            }, "NPCKillsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = $"{stats.npcStats.wolves}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.57 -10.30", OffsetMax = "122.96 15.80" }
            }, "NPCKillsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIBears"), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-21.19 -13.05", OffsetMax = "55.39 13.05" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "BearIcon"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-21.19 -8.51", OffsetMax = "-7.17 5.51" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.57 -36.40", OffsetMax = "122.96 -10.30" }
            }, "NPCKillsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = $"{stats.npcStats.bears}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-126.09 -74.63", OffsetMax = "-11.70 -48.53" }
            }, "NPCKillsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIScientists"), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-23.85 -13.05", OffsetMax = "50.63 13.05" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "ScientistIcon"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-37.34 -7.34", OffsetMax = "-23.85 6.14" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-126.09 -100.74", OffsetMax = "-11.70 -74.63" }
            }, "NPCKillsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = $"{stats.npcStats.scientists}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.57 -74.63", OffsetMax = "122.96 -48.53" }
            }, "NPCKillsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIHelicopters"), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-16.99 -13.05", OffsetMax = "54.59 13.05" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "HelicopterIcon"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32.72 -7.66", OffsetMax = "-18.79 6.26" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.57 -100.74", OffsetMax = "122.96 -74.63" }
            }, "NPCKillsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = $"{stats.npcStats.helicopters}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.34 0.33 0.32 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-125.69 -141.26", OffsetMax = "122.96 -115.16" }
            }, "NPCKillsPanel", "NodeCatagory");

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryText",
                Parent = "NodeCatagory",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIBradleys"), Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-35.91 -13.05", OffsetMax = "44.31 13.05" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "NodeCatagoryIcon",
                Parent = "NodeCatagory",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", "BradleyIcon"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-40.62 -7.85", OffsetMax = "-26.74 6.01" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.43 0.41 0.40 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-125.69 -167.36", OffsetMax = "122.95 -141.26" }
            }, "NPCKillsPanel", "NodeResultPanel");

            container.Add(new CuiElement
            {
                Name = "NodeResult",
                Parent = "NodeResultPanel",
                Components = {
                    new CuiTextComponent { Text = $"{stats.npcStats.bradleys}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.19 -13.05", OffsetMax = "57.19 13.05" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.08 0.07 0.07 0.80" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "304.03 -290.50", OffsetMax = "607.16 271.71" }
            }, "BetterStatsContainer", "WeaponKillsPanel");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.08 0.07 0.07 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-151.56 250.68", OffsetMax = "151.56 281.10" }
            }, "WeaponKillsPanel", "HeaderPanel");

            container.Add(new CuiElement
            {
                Name = "WeaponKillsText",
                Parent = "HeaderPanel",
                Components = {
                    new CuiTextComponent { Text = GetLang("UIWeaponKills"), Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-139.16 -15.21", OffsetMax = "76.82 15.21" }
                }
            });

            float minx = -141.26f; float maxx = -52.87f;
            float miny = 130.81f; float maxy = 238.09f;

            int pagemin = ((page * 12) - 12);
            int pagemax = (page * 12) - 1;

            int index = 0;
            foreach (var weapon in weapons)
            {
                if (index < pagemin || index > pagemax) { index++; continue; }
                if (index != pagemin)
                {
                    int[] resetIndexs = { 3, 6, 9, 12, 15, 18, 21 };
                    if (resetIndexs.Contains(index))
                    {
                        minx = -141.26f;
                        maxx = -52.87f;
                        miny -= 116.35f;
                        maxy -= 116.35f;
                    }
                    else
                    {
                        minx -= -97.07f;
                        maxx -= -97.07f;
                    }
                }

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.34 0.33 0.32 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{minx} {miny}", OffsetMax = $"{maxx} {maxy}" }
                }, "WeaponKillsPanel", "NodeCatagory");

                container.Add(new CuiElement
                {
                    Name = "NodeCatagoryIcon",
                    Parent = "NodeCatagory",
                    Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", weapon.WeaponName), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-35.64 -21.44", OffsetMax = "31.84 46.04" }
                    }
                });

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.43 0.41 0.40 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-44.19 -53.63", OffsetMax = "44.19 -30.24" }
                }, "NodeCatagory", "NodeResultPanel");

                container.Add(new CuiLabel
                {
                    Text = { Color = "1 1 1 1", Text = $"{GetAmount(stats.weaponStats, weapon.WeaponName)}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                }, "NodeResultPanel", "NodeResult");

                container.Add(new CuiButton
                {
                    Button = { Color = $"{(page == 1 ? "0.23 0.29 0.15 1" : "0.58 0.18 0.12 1")}", Command = $"{(page == 1 ? $"reloadstats {userId} next {wipeTable}" : $"reloadstats {userId} prev {wipeTable}")}" },
                    Text = { Text = $"{(page == 1 ? GetLang("UINextPage") : GetLang("UIPreviousPage"))}", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = $"{(page == 1 ? "0.65 0.80 0.38 1" : "0.78 0.59 0.59 1")}" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-141.26 -265.75", OffsetMax = "142.71 -232.44" }
                }, "WeaponKillsPanel", "PageButton");

                index++;
            }

            CuiHelper.AddUi(player, container);
        }
        private void BetterStatsLeaderboard(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "BetterStatsLeaderboard");

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.10 0.10 0.08 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-470.81 -248.57", OffsetMax = "428.05 266.88" }
            }, "Overlay", "BetterStatsLeaderboard");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.08 0.07 0.07 0.8" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-449.43 -47.90", OffsetMax = "449.43 0" }
            }, "BetterStatsLeaderboard", "BetterStatsHeader");

            container.Add(new CuiElement
            {
                Name = "HeaderTitle",
                Parent = "BetterStatsHeader",
                Components = {
                    new CuiTextComponent { Text = "Better Statistics", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "32.4 -18.17", OffsetMax = "211.82 18.17" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.58 0.18 0.12 1", Command = "destroyleaderboard" },
                RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-141.22 -13.03", OffsetMax = "-31.97 16.65" }
            }, "BetterStatsHeader", "CloseButton");

            container.Add(new CuiElement
            {
                Name = "Label_6953",
                Parent = "CloseButton",
                Components = {
                    new CuiTextComponent { Text = "EXIT", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "0.78 0.59 0.59 1" },
                    new CuiRectTransformComponent { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-63.06 -15", OffsetMax = "0 15" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Image_9016",
                Parent = "CloseButton",
                Components = {
                    new CuiRawImageComponent { Color = "0.78 0.59 0.59 1", Sprite = "assets/icons/close.png", Material = "assets/icons/iconmaterial.mat"  },
                    new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "24.16 -7.36", OffsetMax = "38.9 7.36" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.08 0.07 0.07 0.80" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-437.15 -243.01", OffsetMax = "429.36 196.47" }
            }, "BetterStatsLeaderboard", "LeaderboardPanel");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.10 0.10 0.08 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-427.56 -93.26", OffsetMax = "-209.26 -48.33" }
            }, "LeaderboardPanel", "NamePanel");

            container.Add(new CuiElement
            {
                Name = "PlayerStatsText",
                Parent = "NamePanel",
                Components = {
                    new CuiTextComponent { Text = "Name", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.10 0.10 0.08 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-203.04 -93.26", OffsetMax = "-96.53 -48.33" }
            }, "LeaderboardPanel", "KillsPanel");

            container.Add(new CuiElement
            {
                Name = "PlayerStatsText",
                Parent = "KillsPanel",
                Components = {
                    new CuiTextComponent { Text = "Kills", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.10 0.10 0.08 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-91.86 -93.26", OffsetMax = "12.56 -48.33" }
            }, "LeaderboardPanel", "DeathsPanel");

            container.Add(new CuiElement
            {
                Name = "PlayerStatsText",
                Parent = "DeathsPanel",
                Components = {
                    new CuiTextComponent { Text = "Deaths", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.10 0.10 0.08 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "19.19 -93.26", OffsetMax = "123.62 -48.33" }
            }, "LeaderboardPanel", "KDRPanel");

            container.Add(new CuiElement
            {
                Name = "PlayerStatsText",
                Parent = "KDRPanel",
                Components = {
                    new CuiTextComponent { Text = "KDR", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.10 0.10 0.08 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "130.99 -93.26", OffsetMax = "247.19 -48.33" }
            }, "LeaderboardPanel", "AccuracyPanel");

            container.Add(new CuiElement
            {
                Name = "PlayerStatsText",
                Parent = "AccuracyPanel",
                Components = {
                    new CuiTextComponent { Text = "Accuracy", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.10 0.10 0.08 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "253.79 -93.26", OffsetMax = "426.51 -48.33" }
            }, "LeaderboardPanel", "ActionsPanel");

            container.Add(new CuiElement
            {
                Name = "PlayerStatsText",
                Parent = "ActionsPanel",
                Components = {
                    new CuiTextComponent { Text = "Actions", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                }
            });

            CuiHelper.AddUi(player, container);
            HeaderPanel(player);
        }
        private void HeaderPanel(BasePlayer player, bool wipe = true, int page = 1)
        {
            CuiHelper.DestroyUi(player, "HeaderPanel");
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.08 0.07 0.07 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-455.74 -199.29", OffsetMax = "407.98 -154.36" }
            }, "Overlay", "HeaderPanel");

            container.Add(new CuiElement
            {
                Name = "PlayerStatsText",
                Parent = "HeaderPanel",
                Components = {
                    new CuiTextComponent { Text = "Leaderboard Summary", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-413.85 -15.21", OffsetMax = "-211.14 15.21" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = $"{(wipe ? "0.23 0.29 0.15 1" : "0.58 0.18 0.12 1")}", Command = "stats.action stats wipe" },
                Text = { Text = "WIPE", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = $"{(wipe ? "0.65 0.80 0.38 1" : "0.78 0.59 0.59 1")}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-79.84 -16.09", OffsetMax = "-2.25 12.33" }
            }, "HeaderPanel", "WipeButton");

            container.Add(new CuiButton
            {
                Button = { Color = $"{(wipe ? "0.58 0.18 0.12 1" : "0.23 0.29 0.15 1")}", Command = "stats.action stats overall" },
                Text = { Text = "OVERALL", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = $"{(wipe ? "0.78 0.59 0.59 1" : "0.65 0.80 0.38 1")}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "2.40 -16.09", OffsetMax = "79.99 12.33" }
            }, "HeaderPanel", "OverallButton");


            Dictionary<string, PlayerStats> stats = playerStats;
            if (!wipe)
                stats = playerStatsOverall;

            int pageCount = GetPageCount(stats.Count, 10);

            if (page > 1)
                container.Add(new CuiButton
                {
                    Button = { Color = "0.58 0.18 0.12 1", Command = $"stats.action page prev {wipe} {page}" },
                    Text = { Text = "< PREV", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.78 0.59 0.59 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "265.20 -16.09", OffsetMax = "342.79 12.33" }
                }, "HeaderPanel", "PrevPage");

            if (page < pageCount)
                container.Add(new CuiButton
                {
                    Button = { Color = "0.23 0.29 0.15 1", Command = $"stats.action page next {wipe} {page}" },
                    Text = { Text = "NEXT >", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.65 0.80 0.38 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "347.52 -16.09", OffsetMax = "425.12 12.33" }
                }, "HeaderPanel", "NextPage");

            CuiHelper.AddUi(player, container);
            PlayersPanel(player, wipe, page);
        }
        private void PlayersPanel(BasePlayer player, bool wipe = true, int page = 1)
        {
            CuiHelper.DestroyUi(player, "PlayersPanel");

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-452.84 -227.73", OffsetMax = "401.24 108.93" }
            }, "Overlay", "PlayersPanel");

            Dictionary<string, PlayerStats> stats = playerStats;
            if (!wipe)
                stats = playerStatsOverall;

            var newStatsList = stats.OrderByDescending(pStats => pStats.Value.deaths == 0 ? pStats.Value.kills : (float)Math.Round(((float)pStats.Value.kills) / pStats.Value.deaths, 2)).ToList();

            int pagemin = ((page * 10) - 10);
            int pagemax = (page * 10) - 1;

            int index = 0;
            float miny = 134.66f; float maxy = 168.33f;
            foreach (KeyValuePair<string, PlayerStats> pStats in newStatsList)
            {
                if (index < pagemin || index > pagemax) { index++; continue; }
                if (index != pagemin)
                {
                    miny -= 33.67f;
                    maxy -= 33.67f;
                }

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = $"0.10 0.10 0.08 {(index % 2 == 0 ? 0 : 1)}" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-427.04 {miny}", OffsetMax = $"427.04 {maxy}" }
                }, "PlayersPanel", "PlayerPanel");


                container.Add(new CuiElement
                {
                    Name = "Name",
                    Parent = "PlayerPanel",
                    Components = {
                        new CuiTextComponent { Text = pStats.Value.name, Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-424.53 -13.05", OffsetMax = "-210.80 13.05" }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = "Kills",
                    Parent = "PlayerPanel",
                    Components = {
                        new CuiTextComponent { Text = $"{pStats.Value.kills}", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-200.83 -13.05", OffsetMax = "-96.88 13.05" }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = "Deaths",
                    Parent = "PlayerPanel",
                    Components = {
                        new CuiTextComponent { Text = $"{pStats.Value.deaths}", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-90.23 -13.05", OffsetMax = "11.33 13.05" }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = "KDR",
                    Parent = "PlayerPanel",
                    Components = {
                        new CuiTextComponent { Text = $"{(pStats.Value.deaths == 0 ? pStats.Value.kills : (float)Math.Round(((float)pStats.Value.kills) / pStats.Value.deaths, 2))}", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "21.21 -13.05", OffsetMax = "122.78 13.05" }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = "Accuracy",
                    Parent = "PlayerPanel",
                    Components = {
                        new CuiTextComponent { Text = CalculateAccuracy(pStats.Value.bfired, pStats.Value.hits), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "134.81 -13.05", OffsetMax = "246.88 13.05" }
                    }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "0.11 0.25 0.37 1", Command = $"stats {pStats.Key}" },
                    Text = { Text = "View Profile", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.28 0.60 0.83 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "274.43 -13.05", OffsetMax = "403.09 13.05" }
                }, "PlayerPanel", "ViewProfileButton");

                index++;
            }

            CuiHelper.AddUi(player, container);
        }
        private int GetPageCount(int count, int maxPerPage)
        {
            return (int)Math.Ceiling((double)count / maxPerPage);
        }
        #endregion
    }
}