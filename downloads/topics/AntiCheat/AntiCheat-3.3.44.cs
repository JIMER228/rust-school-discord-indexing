using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using Oxide.Game.Rust.Libraries.Covalence;


namespace Oxide.Plugins
{
    [Info("AntiCheat", "Revorck by GrazyCat", "3.3.44", ResourceId = 730)]
    class AntiCheat : RustPlugin
    {
    
    	////////////////////////////////////////////////////////////
        // Plugin References
        ////////////////////////////////////////////////////////////

        [PluginReference]
        Plugin EnhancedBanSystem;

        [PluginReference] 
        Plugin DeadPlayersList;
        
        [PluginReference]
        Plugin Jail;
    	
    	static bool jailExists = false;
    	
        ////////////////////////////////////////////////////////////
        // Cached Fields
        ////////////////////////////////////////////////////////////

        static RaycastHit cachedRaycasthit;
		Dictionary<string, string> deadPlayers = new Dictionary<string, string>();
		Hash<ulong, BasePlayer> cachedPlayers = new Hash<ulong, BasePlayer>();
		
        ////////////////////////////////////////////////////////////
        // Static Fields
        ////////////////////////////////////////////////////////////
        
        static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        
        static DamageTypeList emptyDamage = new DamageTypeList();
        
        static Vector3 VectorDown = new Vector3(0f, -1f, 0f);
        
        static int flyColl;
        static int constructionColl;
        static int bulletmask;
        
        ////////////////////////////////////////////////////////////
        // Fields
        ////////////////////////////////////////////////////////////
        
        Oxide.Plugins.Timer activateTimer;
        
        bool serverInitialized = false;

        static List<BasePlayer> adminList = new List<BasePlayer>();
		
		static bool fpsCheckCalled = false;
        static ConsoleSystem.Arg fpsCaller;
        static List<PlayerHack> fpsCalled = new List<PlayerHack>();
        static double fpsTime;
		
        ////////////////////////////////////////////////////////////
        // Config Fields
        ////////////////////////////////////////////////////////////

        static int authIgnore = 1;
        static int fpsIgnore = 30;
        static bool banFamilyShare = true;
        static int punishType = 1;
		static float resetTime = 60f;

        static bool speedhack = true;
        static bool speedhackPunish = true;
        static int speedhackDetections = 3;
        static float minSpeedPerSecond = 7f;
        static bool speedhackLog = true;

        static bool flyhack = true;
        static bool flyhackPunish = true;
        static int flyhackDetections = 3;
        static bool flyhackLog = true;

        static bool wallhack = true;
        static bool wallhackPunish = true;
        static bool wallhackLog = true;
        static int wallhackDetections = 2;

        static bool wallhackkills = true;
        static bool wallhackkillsLog = true;

        static bool meleespeedhack = true;


        static string multipleNames = "Multiple players found with this name";
        static string noPlayerFound = "No player found with this name";

        ////////////////////////////////////////////////////////////
        // Config Management
        ////////////////////////////////////////////////////////////

        protected override void LoadDefaultConfig() { }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }
        private void CheckCfgFloat(string Key, ref float var)
        {

            if (Config[Key] != null)
                var = Convert.ToSingle(Config[Key]);
            else
                Config[Key] = var;
        }

        void Init()
        {
            CheckCfg<int>("Настройки: Уровень доступа который будет игнорироваться (AuthLevel)", ref authIgnore);
            CheckCfg<int>("Настройки: Наказание: 0 = Кикнуть, 1 = Забанить, 2 = Логировать", ref punishType);
            CheckCfg<int>("Настройки: До какого FPS игнорировать", ref fpsIgnore);
            CheckCfg<bool>("Настройки: Банить Администраторов", ref banFamilyShare);
            CheckCfg<bool>("MeleeSpeed Hack: Включить", ref meleespeedhack);
            CheckCfg<bool>("SpeedHack: Включить", ref speedhack);
            CheckCfg<bool>("SpeedHack: Включить наказания", ref speedhackPunish);
            CheckCfg<bool>("SpeedHack: Включить логирование", ref speedhackLog);
            CheckCfg<int>("SpeedHack: Нарушений до наказания", ref speedhackDetections);
            CheckCfgFloat("SpeedHack: Скорость до наказания", ref minSpeedPerSecond);
            CheckCfg<bool>("Flyhack: Включить", ref flyhack);
            CheckCfg<bool>("Flyhack: Включить наказания", ref flyhackPunish);
            CheckCfg<bool>("Flyhack: Включить логирование", ref flyhackLog);
            CheckCfg<int>("Flyhack: Нарушений до наказания", ref flyhackDetections);
            CheckCfg<bool>("Wallhack: Включить", ref wallhack);
            CheckCfg<bool>("Wallhack: Включить наказания", ref wallhackPunish);
            CheckCfg<bool>("Wallhack: Включить логирование", ref wallhackLog);
            CheckCfg<int>("Wallhack: Нарушений до наказания", ref wallhackDetections);

            CheckCfg<bool>("Wallhack Kills: Включить", ref wallhackkills);
            CheckCfg<bool>("Wallhack Kills: Включить логирование", ref wallhackkillsLog);
            SaveConfig();
        }
        
        ////////////////////////////////////////////////////////////
        // Log Management
        ////////////////////////////////////////////////////////////

        static StoredData storedData;
        static Hash<string, List<AntiCheatLog>> anticheatlogs = new Hash<string, List<AntiCheatLog>>();

        class StoredData
        {
            public HashSet<AntiCheatLog> AntiCheatLogs = new HashSet<AntiCheatLog>();

            public StoredData()
            {
            }
        }

        void OnServerSave()
        {
            SaveData();
        }

        void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("AntiCheatLogs", storedData);
        }

        void LoadData()
        {
            anticheatlogs.Clear();
            try
            {
                storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("AntiCheatLogs");
            }
            catch
            {
                storedData = new StoredData();
            }
            foreach (var thelog in storedData.AntiCheatLogs)
            {
                if (anticheatlogs[thelog.userid] == null)
                    anticheatlogs[thelog.userid] = new List<AntiCheatLog>();
                (anticheatlogs[thelog.userid]).Add(thelog);
            }
        }

        public class AntiCheatLog
        {
            public string userid;
            public string fx;
            public string fy;
            public string fz;
            public string tx;
            public string ty;
            public string tz;
            public string td;
            public string lg;

            Vector3 frompos;
            Vector3 topos;
			
			public AntiCheatLog()
            {
            }
			
            public AntiCheatLog(string userid, string logType, Vector3 frompos, Vector3 topos)
            {
                this.userid = userid;
                this.fx = frompos.x.ToString();
                this.fy = frompos.y.ToString();
                this.fz = frompos.z.ToString();
                this.tx = topos.x.ToString();
                this.ty = topos.y.ToString();
                this.tz = topos.z.ToString();
                this.td = logType;
                this.lg = LogTime().ToString();
            }

            public Vector3 FromPos()
            {
                if (frompos == default(Vector3))
                    frompos = new Vector3(float.Parse(fx), float.Parse(fy), float.Parse(fz));
                return frompos;
            }

            public Vector3 ToPos()
            {
                if (topos == default(Vector3))
                    topos = new Vector3(float.Parse(tx), float.Parse(ty), float.Parse(tz));
                return topos;
            }
        }

        static void AddLog(string userid, string logType, Vector3 frompos, Vector3 topos)
        {
            if (anticheatlogs[userid] == null)
                anticheatlogs[userid] = new List<AntiCheatLog>();
            AntiCheatLog newlog = new AntiCheatLog(userid, logType, frompos, topos);
            (anticheatlogs[userid]).Add(newlog);
            storedData.AntiCheatLogs.Add(newlog);
        }
		
		static double LogTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }


        ////////////////////////////////////////////////////////////
        // Oxide Hooks
        ////////////////////////////////////////////////////////////

        ////////////////////////////////////////////////////////////
        // Plugin Initialization
        ////////////////////////////////////////////////////////////
        int privColl;
        void Loaded()
        {
            constructionColl = UnityEngine.LayerMask.GetMask(new string[] 
			{ 
			  "Construction" 			  
			});
            privColl = UnityEngine.LayerMask.GetMask(new string[] 
			{ 
			  "Construction",
			  "Deployed",
			  "Prevent Building"
			});
            flyColl = UnityEngine.LayerMask.GetMask(new string[]
			{ 
			  "Construction",
			  "Deployed",
			  "Tree",
			  "Terrain",
			  "Resource",
			  "World",
			  "Water",
			  "Default",
			  "Prevent Building"
			});
 
            if (!permission.PermissionExists("anticheat.cananticheat")) permission.RegisterPermission("anticheat.cananticheat", this);
        }
		
		
		////////////////////////////////////////////////////////////
        // Server Initialization
        ////////////////////////////////////////////////////////////
        
        void ServerInitialized()
        {
            serverInitialized = true;
            if(Jail != null) jailExists = true;
            
            LoadData();

            timer.Once(1f, () => RefreshPlayers());

            //ConsoleSystem.Run(ConsoleSystem.Option.Unrestricted, "antihack.enabled false");
        }
        
        ////////////////////////////////////////////////////////////
        // Plugin Unload
        ////////////////////////////////////////////////////////////
        
        void Unload()
        {
            DestroyAll<PlayerHack>();
            DestroyAll<ColliderCheckTest>();
            DestroyAll<PlayerLog>();
            if (activateTimer != null)
                activateTimer.Destroy();
        }
        
        void DestroyAll<T>()
        {
            UnityEngine.Object[] objects = GameObject.FindObjectsOfType(typeof(T));
            if (objects != null)
                foreach (UnityEngine.Object gameObj in objects)
                    GameObject.Destroy(gameObj);
        }
        
        ////////////////////////////////////////////////////////////
        // OnPlayerRespawned
        // Called when a player respawns (after death)
        ////////////////////////////////////////////////////////////
        void OnPlayerRespawned(BasePlayer player)
        {
            RefreshPlayer(player);
        }
        
        ////////////////////////////////////////////////////////////
        // OnPlayerInit
        // Called when a player enters the world
        ////////////////////////////////////////////////////////////
        void OnPlayerInit(BasePlayer player)
        {
        	if(player.net != null && player.net.connection != null)
        	{
        		RefreshPlayer( player );
            	if (isAdmin(player)) { if (!adminList.Contains(player)) adminList.Add(player); }
            }
        }
        
        ////////////////////////////////////////////////////////////
        // OnPlayerDisconnected
        // Called when a player disconnects from the server
        ////////////////////////////////////////////////////////////
        void OnPlayerDisconnected(BasePlayer player) 
        {
            if (adminList.Contains(player)) adminList.Remove(player);
        }
        
        ////////////////////////////////////////////////////////////
        // Random Functions
        ////////////////////////////////////////////////////////////
        
        bool isAdmin( BasePlayer player )
        {
        	if (player.net.connection.authLevel > 0) return true;
        	return permission.UserHasPermission(player.userID.ToString(), "anticheat.cananticheat");
        }
        static double CurrentTime()
		{
			return DateTime.UtcNow.Subtract(epoch).TotalMilliseconds;
		}
        static double CurrentTimeSec()
        {
            return DateTime.UtcNow.Subtract(epoch).TotalSeconds;
        }

        ////////////////////////////////////////////////////////////
        // Refresh Players
        ////////////////////////////////////////////////////////////

        void RefreshPlayers()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
            	RefreshPlayer( player );
            	if( isAdmin(player)) { if (!adminList.Contains(player)) adminList.Add(player); }
            }
        }
        
        void RefreshPlayer(BasePlayer player)
        {
			if (isAdmin(player)) return;
			if (wallhack) MakeTestCollider( player );
			if (speedhack || flyhack)
				if (player.GetComponent<PlayerHack>() == null) 
					player.gameObject.AddComponent<PlayerHack>();
        }
		
        ////////////////////////////////////////////////////////////
        // PlayerHack class
        ////////////////////////////////////////////////////////////

        public class PlayerHack : MonoBehaviour
        {
            public BasePlayer player;
            
            public Vector3 lastPosition;
            public Vector3 currentDirection;
            
            public bool isonGround;
			public bool wasGround = true;
            
            
            public float Distance3D;
            public float VerticalDistance;
            public float deltaTick;
			public float flyHackDetections = 0f;
            public float speedHackDetections = 0f;
            
            public double currentTick;
            public double lastTick;
            public double lastTickFly;
            public double lastTickSpeed;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                InvokeRepeating("CheckPlayer", 1f, 1f);
                lastPosition = player.transform.position;
            }
            void CheckPlayer()
            {
                if (!player.IsConnected()) GameObject.Destroy(this);
                currentTick = CurrentTime();
                deltaTick = (float)((currentTick - lastTick)/1000.0);
                Distance3D = Vector3.Distance(player.transform.position, lastPosition)/deltaTick;
                VerticalDistance = (player.transform.position.y - lastPosition.y)/deltaTick;
                currentDirection = (player.transform.position - lastPosition).normalized;
                isonGround = player.IsOnGround();

                if (!player.IsWounded() && !player.IsDead() && !player.IsSleeping() && deltaTick < 1.1f && Performance.current.frameRate > fpsIgnore)
                    CheckForHacks(this);

                lastPosition = player.transform.position;

                if (fpsCheckCalled)
                    if (!fpsCalled.Contains(this))
                    {
                        fpsCalled.Add(this);
                        fpsTime += (CurrentTime() - currentTick);
                    }

                lastTick = currentTick;
            }
        }
        static void CheckForHacks(PlayerHack hack)
        {
            if (speedhack)
                CheckForSpeedHack(hack);
            if (flyhack)
                CheckForFlyhack(hack);
        }

        ////////////////////////////////////////////////////////////
        // Wallhack related
        ////////////////////////////////////////////////////////////
		
		Hash<ulong, ColliderCheckTest> playerWallcheck = new Hash<ulong, ColliderCheckTest>();
		
		static Hash<BuildingBlock, float> createdBuilding = new Hash<BuildingBlock, float>();
		static Hash<uint, float> DoorCheck = new Hash<uint, float>();
		static Hash<BasePlayer, float> lastWallhack = new Hash<BasePlayer, float>();
		static Hash<BasePlayer, int> wallhackDetec = new Hash<BasePlayer, int>();

		static bool isOpen(BuildingBlock block)
		{
			
			if (block.net != null && block.net.ID != null)
				if (Time.realtimeSinceStartup - DoorCheck[block.net.ID] < 6f)
					return true;
			if(block.blockDefinition.hierachyName.StartsWith("block"))
				return true;
			
			switch(block.blockDefinition.hierachyName)
			{
				case "wall.doorway":
					foreach( Collider collider in UnityEngine.Physics.OverlapSphere(block.transform.position, 2f) )
					{
						if(collider.GetComponentInParent<Door>() != false)
						{
							if (Time.realtimeSinceStartup - DoorCheck[collider.GetComponentInParent<BuildingBlock>().net.ID] < 6f)
							{
								return true;
							}
						}
					}
				break;
				default:
				break;
			}
			
			return false;
		}
			
		void OnDoorOpened(Door door, BasePlayer player) { DoorCheck[door.net.ID] = Time.realtimeSinceStartup; }

        void OnDoorClosed(Door door, BasePlayer player) { DoorCheck[door.net.ID] = Time.realtimeSinceStartup; }
		
		public class ColliderCheckTest : MonoBehaviour
        {
            public BasePlayer player;
			Hash<Collider, Vector3> entryPosition = new Hash<Collider, Vector3>();
			SphereCollider col;
			public float teleportedBack;
			public Collider lastCollider;
			
            void Awake()
            {
                player = transform.parent.GetComponent<BasePlayer>();

                col = gameObject.AddComponent<SphereCollider>();
                col.radius = 0.1f;
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.5f, 0f);
            }
			void OnTriggerEnter(Collider collision)
            {
            	if( Time.realtimeSinceStartup < teleportedBack + 0.2f && collision != lastCollider ) return;
				if( hasBuildingPrivileges(player ) ) return;
                if (collision.GetComponent<MeshCollider>() == null) return;
                if (collision.gameObject.name != "Mesh Collider Batch") return;
                MeshColliderBatch meshcoll = collision.GetComponent<MeshColliderBatch>();
                if (meshcoll == null) return;
                entryPosition[collision] = player.transform.position;
			}

            void OnTriggerExit(Collider collision)
            {
                if (entryPosition.ContainsKey(collision))
                {
                    MeshColliderBatch meshcoll = collision.GetComponent<MeshColliderBatch>();
                    BaseEntity targetent = GetCollEntity(entryPosition[collision], player.transform.position);
                    if (targetent != null)
                    {
                        BuildingBlock block = targetent.GetComponent<BuildingBlock>();
                        if (block != null)
                        {
                            if (!block.gameObject.name.Contains("foundation.steps") && !block.gameObject.name.Contains("block.halfheight.slanted"))
                            {
                                Interface.GetMod().LogWarning(string.Format("Анти-Чит: У {0} был обнаружен wallhacking", player.displayName));

                                SendDetection(string.Format("{0} - {1}", collision.ToString(), block.gameObject.name));

                                ForcePlayerBack(this, collision, entryPosition[collision], player.transform.position);

                                if (Time.realtimeSinceStartup - lastWallhack[player] < 10f)
                                {
                                    SendDetection(string.Format("{0} - {1} Обнаружен с : Wallhack", player.userID.ToString(), player.displayName));

                                    if (wallhackLog)
                                        AddLog(player.userID.ToString(), "wall", entryPosition[collision], player.transform.position);
                                    if (wallhackPunish)
                                    {
                                        wallhackDetec[player]++;
                                        if (wallhackDetec[player] >= wallhackDetections)
                                        {
                                            Punish(player, string.Format("rWallhack"));
                                        }
                                    }
                                }
                                else if (wallhackPunish)
                                    wallhackDetec[player] = 0;

                                lastWallhack[player] = Time.realtimeSinceStartup;
                            }
                        }
                    }
                    entryPosition.Remove(collision);
                }
            }
            

            void OnDestroy()
            {
                GameObject.Destroy(gameObject);
                GameObject.Destroy(col);
            }
        }
       	static bool hasBuildingPrivileges(BasePlayer player)
       	{
       		return player.HasPlayerFlag(BasePlayer.PlayerFlags.HasBuildingPrivilege);
       	}
        public static BaseEntity GetCollEntity(Vector3 entry, Vector3 exist)
        {
            var rayArray = Physics.RaycastAll(exist, entry, Vector3.Distance(entry, exist), constructionColl);
            for(int i = 0; i < rayArray.Length; i++)
            {
                return rayArray[i].GetEntity();
            }
            return null;
        }
        static void ForcePlayerBack(ColliderCheckTest colcheck, Collider collision, Vector3 entryposition, Vector3 exitposition)
        {
        	Vector3 rollBackPosition = GetRollBackPosition(entryposition, exitposition, 4f);
        	Vector3 rollDirection = (entryposition - exitposition).normalized;
            foreach( RaycastHit rayhit in UnityEngine.Physics.RaycastAll( rollBackPosition, (exitposition - entryposition).normalized, 5f ))
            {
            	if(rayhit.collider == collision)
            	{
            		rollBackPosition = rayhit.point + rollDirection*1f;
            	}
            }
            colcheck.teleportedBack = Time.realtimeSinceStartup;
            colcheck.lastCollider = collision;
            ForcePlayerPosition(colcheck.player, rollBackPosition );
        }
        static Vector3 GetRollBackPosition( Vector3 entryposition, Vector3 exitposition, float distance)
        {
        	distance = Vector3.Distance(exitposition, entryposition) + distance;
            var direction = (entryposition - exitposition).normalized;
            return (exitposition + (direction * distance));
        }

        static void ForcePlayerPosition(BasePlayer player, Vector3 destination)
        {
            player.MovePosition(destination);
            player.ClientRPCPlayer(null, player, "ForcePositionTo", destination);
        }

        void OnEntityBuilt(Planner planner, GameObject gameobject)
        {
            if (!wallhack) return;
            if (!serverInitialized) return;
            BuildingBlock buildingblock = gameobject.GetComponentInParent<BuildingBlock>();
            if (buildingblock == null) return;
            createdBuilding[buildingblock] = Time.realtimeSinceStartup;
        }
        
        void MakeTestCollider(BasePlayer player, bool force = false)
        {
        	if(player == null) return;
        	
        	if(playerWallcheck[player.userID] != null)
        	{
        		GameObject.Destroy(playerWallcheck[player.userID]);
        		playerWallcheck[player.userID] = null;
        	}
        	
        	if(player.transform != null && player.transform.position != null)
        	{
        		if(player.net.connection.authLevel < 1 || force)
        		{
					var newObject = new GameObject("ColliderTest");
					newObject.transform.position = player.transform.position;
					newObject.transform.parent = player.transform;
					playerWallcheck[player.userID] = newObject.AddComponent<ColliderCheckTest>();
        		}
        	}
        }

        ////////////////////////////////////////////////////////////
        // Speedhack related
        ////////////////////////////////////////////////////////////

        static void CheckForSpeedHack(PlayerHack hack)
        {
            if (hack.Distance3D < minSpeedPerSecond) return;
            if (hack.VerticalDistance < -8f) return;
            if (hack.lastTickSpeed == hack.lastTick)
            {
                hack.speedHackDetections++;
                if (speedhackLog)
                    AddLog(hack.player.userID.ToString(), "speed", hack.lastPosition, hack.player.transform.position);
                SendDetection(string.Format("{0} - {1} Обнаружен с : Speedhack ({2}м/с)", hack.player.userID.ToString(), hack.player.displayName, hack.Distance3D.ToString()));
                if (hack.speedHackDetections >= speedhackDetections)
                {
                    if (speedhackPunish)
                        Punish(hack.player, string.Format("rSpeedhack ({0}м/с)", hack.Distance3D.ToString()));
                }

            }
            else
            {
                hack.speedHackDetections = 0f;
            }
            hack.lastTickSpeed = hack.currentTick;
        }

        ////////////////////////////////////////////////////////////
        // Flyhack related
        ////////////////////////////////////////////////////////////

        static void CheckForFlyhack(PlayerHack hack)
        { 
            if (hack.isonGround) return;
            if (hack.Distance3D != 0f && hack.VerticalDistance < -5f && hack.VerticalDistance/hack.Distance3D < -0.5) return;
            if (UnityEngine.Physics.Raycast(hack.player.transform.position, VectorDown, 3f)) { hack.wasGround = true; return; }
            foreach(Collider col in UnityEngine.Physics.OverlapSphere(hack.player.transform.position, 2f, flyColl)) { hack.wasGround = true; return; }
            if (hack.player.WaterFactor() > 0) return;
            if (!hack.wasGround && hack.lastTickFly == hack.lastTick)
            {
                hack.flyHackDetections++;
                if (flyhackLog)
                    AddLog(hack.player.userID.ToString(), "fly", hack.lastPosition, hack.player.transform.position);
                SendDetection(string.Format("{0} - {1} обнаружен с : Flyhack ({2}м/с)", hack.player.userID.ToString(), hack.player.displayName, hack.Distance3D.ToString()));
                if (hack.flyHackDetections >= flyhackDetections)
                {
                    if (flyhackPunish)
                        Punish(hack.player, string.Format("rFlyhack ({0}м/с)", hack.Distance3D.ToString()));
                }
            }
            else
            {
                hack.flyHackDetections = 0f;
            }
            hack.wasGround = false;
            hack.lastTickFly = hack.currentTick;
        }
		
		////////////////////////////////////////////////////////////
        // Wallhack Kills related
        ////////////////////////////////////////////////////////////
		
        void WallhackKillCheck(BasePlayer player, BasePlayer attacker, HitInfo hitInfo)
        {
            if (Physics.Linecast(attacker.eyes.position, hitInfo.HitPositionWorld, out cachedRaycasthit, bulletmask))
            {
            	BuildingBlock block = cachedRaycasthit.collider.GetComponentInParent<BuildingBlock>();
                if (block != null)
                {
                	if(block.blockDefinition.hierachyName == "wall.window") return;
                	
                    CancelDamage(hitInfo);
                    if (Time.realtimeSinceStartup - lastWallhack[attacker] > 0.5f)
                    {
                        lastWallhack[attacker] = Time.realtimeSinceStartup;
                        SendDetection(string.Format("{0} - {1} Обнаружено убийство {2} через стену", attacker.userID.ToString(), attacker.displayName, player.displayName));

                        if (wallhackkillsLog)
                            AddLog(attacker.userID.ToString(), "wallkill", attacker.eyes.position, hitInfo.HitPositionWorld);
                    }
                }
            }
        }


        void OnBasePlayerAttacked(BasePlayer player, HitInfo hitInfo)
        {
            if (!wallhackkills) return;
            if (player.IsDead()) return;
            if (hitInfo.Initiator == null) return;
            if (player.health - hitInfo.damageTypes.Total() > 0f) return;
            BasePlayer attacker = hitInfo.Initiator.ToPlayer();
            if (attacker == null) return;
            if (attacker == player) return;
            WallhackKillCheck(player, attacker, hitInfo);
        }
        void CancelDamage(HitInfo hitinfo)
        {
            hitinfo.damageTypes = emptyDamage;
            hitinfo.HitEntity = null;
        }

        ////////////////////////////////////////////////////////////
        // Anti OverKill related
        ////////////////////////////////////////////////////////////

        public Hash<BasePlayer, double> lastAttack = new Hash<BasePlayer, double>();
        public Hash<BasePlayer, int> attackSpeedDetections = new Hash<BasePlayer, int>();

        void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            var thetime = Time.realtimeSinceStartup;
            if (attacker == null) return;
            if (info.Weapon == null) return;
            
            if (meleespeedhack)
            {
                BaseMelee melee = info.Weapon.GetComponent<BaseMelee>();
                if (melee == null) return;
                double currenttime = CurrentTimeSec();
                if ((currenttime - lastAttack[attacker]) < melee.repeatDelay - 0.2)
                {
                        CancelDamage(info);
                        attackSpeedDetections[attacker]++;
                        if(attackSpeedDetections[attacker] > 2)
                            SendDetection(string.Format("{0} - {1} Был обнаружен слишком быстро - @ {2}", attacker.userID.ToString(), attacker.displayName.ToString(), attacker.transform.position.ToString()));
                }
                else
                    attackSpeedDetections[attacker] = 0;
                lastAttack[attacker] = currenttime;
            }
        }
        
        object OnCupboardAuthorize(BuildingPrivlidge priv, BasePlayer player)
        {
            RaycastHit rayhit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out rayhit, 3f, privColl)) { SendDetection(string.Format("{0} - {1} was detected trying to take buildingpriv out of reach - @ {2}", player.userID.ToString(), player.displayName.ToString(), player.transform.position.ToString()));  AddLog(player.userID.ToString(), "privilege", player.eyes.position, priv.transform.position); return false; }
            BaseEntity hitentity = rayhit.GetEntity();
            if (hitentity == null) { SendDetection(string.Format("{0} - {1} was detected trying to take buildingpriv out of reach - @ {2}", player.userID.ToString(), player.displayName.ToString(), player.transform.position.ToString())); AddLog(player.userID.ToString(), "privilege", player.eyes.position, priv.transform.position); return false; }
            BuildingPrivlidge hitpriv = hitentity.GetComponent<BuildingPrivlidge>();
            if (hitpriv == null) { SendDetection(string.Format("{0} - {1} was detected trying to take buildingpriv out of reach - @ {2}", player.userID.ToString(), player.displayName.ToString(), player.transform.position.ToString())); AddLog(player.userID.ToString(), "privilege", player.eyes.position, priv.transform.position); return false; }
            if (hitpriv != priv) { SendDetection(string.Format("{0} - {1} was detected trying to take buildingpriv out of reach - @ {2}", player.userID.ToString(), player.displayName.ToString(), player.transform.position.ToString())); AddLog(player.userID.ToString(), "privilege", player.eyes.position, priv.transform.position); return false; }
            return null;
            
        }
        
        ////////////////////////////////////////////////////////////
        // Admin Chat related
        ////////////////////////////////////////////////////////////

        static void SendDetection(string msg)
        {
            foreach (BasePlayer player in adminList)
            {
                if (player != null && player.net != null)
                {
                    player.SendConsoleCommand("chat.add", new object[] { 0, msg});
                }
            }
            Interface.GetMod().LogWarning(msg);
        }
        static void SendMsgAdmin(string msg)
        {
            foreach (BasePlayer player in adminList)
            {
                if (player != null && player.net != null)
                {
                    player.SendConsoleCommand("chat.add", new object[] { 0, msg});
                }
            }
        }

        

        ////////////////////////////////////////////////////////////
        // Punish a player
        ////////////////////////////////////////////////////////////

        void Ban(object source, BasePlayer target, string msg, bool theboolean)
        {
            if (EnhancedBanSystem != null) return;
            ServerUsers.Set(target.userID, ServerUsers.UserGroup.Banned, target.displayName, msg);
            ServerUsers.Save();

			//ConsoleSystem.Broadcast("chat.add", new object[] { 0, "<color=orange>AntiCheat:</color> " + msg });

			PrintWarning(string.Format("{0}[{1}] banned by AntiCheat", target.displayName, target.userID));
            Network.Net.sv.Kick(target.net.connection, "Banned for hacking");
        } 

        static void Punish(BasePlayer player, string msg)
        {
            if (player.net.connection.authLevel < authIgnore)
            {
                if (punishType == 1)
                {
                    if (banFamilyShare)
                        if (player.net.connection.ownerid != player.userID)
                        {
                            ServerUsers.Set(player.net.connection.ownerid, ServerUsers.UserGroup.Banned, player.displayName, msg);
                            ServerUsers.Save();
                        }
                    Interface.GetMod().CallHook("Ban", null, player, msg, false);
                }
                else if(punishType == 2 && jailExists )
                {
                	Interface.CallHook("AddPlayerToJail", player, -1);
                	Interface.CallHook("SendPlayerToJail", player);
                }
                else
                    player.Kick(msg);
            }
            else
            {
                GameObject.Destroy(player.GetComponent<PlayerHack>());
            }
        }

        bool hasAccess(BasePlayer player)
        {
            if (player == null) return false;
            if (player.net.connection.authLevel > 0) return true;
            return permission.UserHasPermission(player.userID.ToString(), "anticheat.cananticheat");
        }
        
        void RefreshBasePlayers()
		{
			cachedPlayers.Clear();
            foreach( BasePlayer player in Resources.FindObjectsOfTypeAll(typeof(BasePlayer)) )
            {
            	cachedPlayers.Add(player.userID, player);
            }
		}
        
		bool FindPlayerByID(string userID, out string targetname)
		{
			ulong userid;
            targetname = string.Empty;
            if (!(userID.Length == 17 && ulong.TryParse(userID, out userid)))
            	return false;
			if(cachedPlayers[userid] != null) targetname = (cachedPlayers[userid]).displayName;
			if(targetname != string.Empty)
				return true;
            if (deadPlayers == null)
                return false;
            if(!deadPlayers.ContainsKey(userID))
            	return false;
            targetname = deadPlayers[userID];
            return true;
		}	
		
        bool FindPlayerByName(string name, out string targetid, out string targetname)
        {
            ulong userid;
            targetid = string.Empty;
            targetname = string.Empty;
            if (name.Length == 17 && ulong.TryParse(name, out userid))
            {
                targetid = name;
                return true;
            }

            foreach (BasePlayer player in Resources.FindObjectsOfTypeAll<BasePlayer>())
            {
                if (player.displayName == name)
                {
                    targetid = player.userID.ToString();
                    targetname = player.displayName;
                    return true;
                }
                if (player.displayName.Contains(name))
                {
                    if (targetid == string.Empty)
                    {
                        targetid = player.userID.ToString();
                        targetname = player.displayName;
                    }
                    else
                    {
                        targetid = multipleNames;
                    }
                }
            }
            if (targetid == multipleNames)
                return false;
            if (targetid != string.Empty)
                return true;
            targetid = noPlayerFound;
            if (DeadPlayersList == null)
                return false;
            deadPlayers = DeadPlayersList.Call("GetPlayerList", null) as Dictionary<string, string>;
            if (deadPlayers == null)
                return false;

            foreach (KeyValuePair<string, string> pair in deadPlayers)
            {
                if (pair.Value == name)
                {
                    targetid = pair.Key;
                    targetname = pair.Value;
                    return true;
                }
                if (pair.Value.Contains(name))
                {
                    if (targetid == noPlayerFound)
                    {
                        targetid = pair.Key;
                        targetname = pair.Value;
                    }
                    else
                    {
                        targetid = multipleNames;
                    }
                }
            }
            if (targetid == multipleNames)
                return false;
            if (targetid != noPlayerFound)
                return true;
            return false;
        }

        ////////////////////////////////////////////////////////////
        // Log Class
        ////////////////////////////////////////////////////////////
        public class AcLog
        {
            public Vector3 frompos;
            public string message;
            public Vector3 topos;

            public AcLog(Vector3 frompos, Vector3 topos, string message )
            {
                this.frompos = frompos;
                this.topos = topos;
                this.message = message;
            }
        }


        public class PlayerLog : MonoBehaviour
        {
            public BasePlayer player;
            public Vector3 lastPosition;
            public List<AcLog> logs = new List<AcLog>();
            

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                InvokeRepeating("CheckLogs", 1f, 2f);
            }
            void CheckLogs()
            {
                if (!player.IsConnected()) { GameObject.Destroy(this); return; }
                foreach(AcLog log in logs)
                {
                    player.SendConsoleCommand("ddraw.arrow", 2f, UnityEngine.Color.red, log.frompos, log.topos, 0.5f);
                    if (log.message != string.Empty)
                    {
                        
                        player.SendConsoleCommand("ddraw.text", 2f, UnityEngine.Color.white, log.frompos, log.message);
                    }

                }
            }
            public void Clear()
            {
                logs.Clear();
            }
            public void AddLog(AntiCheatLog aclog, string targetid)
            {
                string detectionText = string.Empty;
                switch (aclog.td)
                {
                    case "speed":
                        detectionText = string.Format("{0} - speed - {1}м/с", targetid, Vector3.Distance(aclog.ToPos(), aclog.FromPos()).ToString());
                        break;
                    case "cupboard":
                        detectionText = string.Format("{0} - {1}м away", targetid, Vector3.Distance(aclog.ToPos(), aclog.FromPos()).ToString());
                        break;
                    case "itemspawn":
                        detectionText = string.Format("{0} - item spawn", targetid);
                        break;
                    case "fly":
                        detectionText = string.Format("{0} - fly - {1}м/с", targetid, Vector3.Distance(aclog.ToPos(), aclog.FromPos()).ToString());
                        break;
                    case "wall":
                        detectionText = string.Format("{0} - wall", targetid);
                        break;
                    case "wallkill":
                        detectionText = string.Format("{0} - wallkill", targetid);
                    break;
                    default:

                        break;
                }
                logs.Add(new AcLog(aclog.FromPos(), aclog.ToPos(), detectionText));
            }
        }
		
        ////////////////////////////////////////////////////////////
        // Chat Commands
        ////////////////////////////////////////////////////////////
		
        [ChatCommand("ac")]
        void cmdChatAC(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, "У вас нет доступа к этой команде"); return; }
            if (args == null || args.Length < 2)
            {
                if (player.GetComponent<PlayerLog>())
                {
                    SendReply(player, "Выключен просмотр логов Анти-Чита.");
                    GameObject.Destroy(player.GetComponent<PlayerLog>());
                    return;
                }
                SendReply(player, "/ac player PLAYERNAME/STEAMID => Показать все обнаруженные хаки, сделанные этим игроком");
                SendReply(player, "/ac radius RADIUS => Показать все обнаруженные хаки в этом радиусе.");
                return;
            }

            if (args[0].ToLower() == "player")
            {
                PlayerLog playerlog = player.GetComponent<PlayerLog>();
                if (playerlog == null)
                    playerlog = player.gameObject.AddComponent<PlayerLog>();
                string targetid = string.Empty;
                string targetname = string.Empty;
                if (!FindPlayerByName(args[1], out targetid, out targetname))
                {
                    SendReply(player, targetid);
                    return;
                }
                if (anticheatlogs[targetid] == null || (anticheatlogs[targetid]).Count == 0)
                {
                    SendReply(player, string.Format("{0} {1} - Не имеет обнаруженных хаков", targetid, targetname));
                    return;
                }
                SendReply(player, string.Format("{0} {1} - имеет {2} обнаруженных хаков", targetid, targetname, (anticheatlogs[targetid]).Count.ToString()));
                string detectionText = string.Empty;
                foreach (AntiCheatLog aclog in anticheatlogs[targetid])
                {
                    playerlog.AddLog(aclog, targetid);
                }
                SendReply(player, string.Format("/ac_tp NUMBER, Для телепортации к обнаруженному нарушению (0-{0})", (playerlog.logs.Count - 1).ToString()));
            } 
            else if (args[0].ToLower() == "radius")
            {
                PlayerLog playerlog = player.GetComponent<PlayerLog>();
                if (playerlog == null)
                    playerlog = player.gameObject.AddComponent<PlayerLog>();
                float radius = 20f;
                if(!float.TryParse(args[1],out radius))
                {
                    SendReply(player, "/ac radius XXX");
                    return;
                }
                string detectionText = string.Empty;
                playerlog.Clear();
                foreach ( KeyValuePair<string, List<AntiCheatLog>> pair in anticheatlogs)
                {
                    foreach(AntiCheatLog aclog in pair.Value)
                    {
                        if(Vector3.Distance(player.transform.position, aclog.FromPos()) < radius )
                        {
                            playerlog.AddLog(aclog, pair.Key);
                        }
                    }
                }
                SendReply(player, string.Format("{0} Нарушения обнаружены в радиусе {1}м вокруг вас",playerlog.logs.Count.ToString(),radius.ToString()));
                SendReply(player, string.Format("/ac_tp NUMBER, Для телепортации к обнаруженному нарушению (0-{0})",(playerlog.logs.Count-1).ToString()));
            } 
            else
            {
                SendReply(player, string.Format("Такого аргумента: \"{0}\" не существует", args[0]));
            }
        }
        
		[ChatCommand("ac_tp")]
        void cmdChatACTP(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, "У вас нет доступа к этой команде"); return; }
            PlayerLog playerlog = player.GetComponent<PlayerLog>();
            if (playerlog == null)
        	{
            	SendReply(player, "Вы должны использовать \n/ac player NAME/STEAMID или\n/ac radius XXX\nперед использованием этой команды"); 
                return;
            }
            if(playerlog.logs == null  || playerlog.logs.Count == 0)
            {
            	SendReply(player, "Не удалось найти журналы в текущем списке, используйте /ac first");
            	return;
            }
            if(args.Length == 0)
            {
            	SendReply(player, string.Format("Вы должны выбрать номер обнаруженного нарушения, к которому хотите телепортироваться (0-{0})",(playerlog.logs.Count-1).ToString()));
            	return;
            }
            int lognumber = 0;
            if(!int.TryParse(args[0], out lognumber))
            {
            	SendReply(player, string.Format("Вы должны выбрать номер обнаруженного нарушения, к которому хотите телепортироваться (0-{0})",(playerlog.logs.Count-1).ToString()));
            	return;
            }
            if(lognumber < 0 || lognumber >= playerlog.logs.Count)
            {
            	SendReply(player, string.Format("Вы должны выбрать число обнаруженного нарушения между 0 и {0}",(playerlog.logs.Count-1).ToString()));
            	return;
            }
            ForcePlayerPosition(player, playerlog.logs[lognumber].frompos);
            SendReply(player, string.Format("{0} - {1} - {2}", playerlog.logs[lognumber].message.ToString(), playerlog.logs[lognumber].frompos.ToString(), playerlog.logs[lognumber].topos.ToString()));
        }
		
        [ChatCommand("ac_list")]
        void cmdChatACList(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, "У вас нет доступа к этой команде"); return; }
            string targetname = string.Empty;
            deadPlayers = DeadPlayersList?.Call("GetPlayerList", null) as Dictionary<string, string>;
            RefreshBasePlayers();
            foreach (KeyValuePair<string, List<AntiCheatLog>> pair in anticheatlogs)
            {
            	targetname = string.Empty;
            	if(!FindPlayerByID(pair.Key, out targetname))
            		targetname = "Unknown";
                SendReply(player, string.Format("{0} - {1} - {2} обнаруженного нарушения", pair.Key, targetname, pair.Value.Count.ToString()));
            }

            SaveData();
        }

        [ChatCommand("ac_remove")]
        void cmdChatACRemove(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, "У вас нет доступа к этой команде"); return; }
            string targetid = string.Empty;
            string targetname = string.Empty;
            if (!FindPlayerByName(args[0], out targetid, out targetname))
            {
                SendReply(player, targetid);
                return;
            }
            if (anticheatlogs[targetid] == null || (anticheatlogs[targetid]).Count == 0)
            {
                SendReply(player, string.Format("{0} {1} - Не имеет обнаруженных хаков", targetid, targetname));
                return;
            }
           
            foreach (AntiCheatLog aclog in anticheatlogs[targetid])
            {
                storedData.AntiCheatLogs.Remove(aclog);
            }
            anticheatlogs.Remove(targetid);
            SendReply(player, string.Format("Удалено: {0} {1} логов Анти-Чита", targetid, targetname));
            SaveData();
        }

        [ChatCommand("ac_reset")]
        void cmdChatACReset(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, "У вас нет доступа к этой команде"); return; }
            anticheatlogs.Clear();
            storedData.AntiCheatLogs.Clear();
            SaveData();
            SendReply(player, "Анти-Чит: Логи были сброшены");
        }
        
        [ChatCommand("actestsr")]
        void ttest(BasePlayer player, string command, string[] args)
        {
            MakeTestCollider(player, true);
        }
        ////////////////////////////////////////////////////////////
        // Console Commands
        ////////////////////////////////////////////////////////////

        [ConsoleCommand("ac.fps")]
        void cmdConsoleAcFPS(ConsoleSystem.Arg arg)
        {
            if (arg.connection != null)
            {
                if (arg.connection.authLevel < 1)
                {
                    SendReply(arg, "У вас нет доступа к этой команде");
                    return;
                }
            }
            SendReply(arg, "Ожидайте.... Анти-Чит проверяет всех игроков");
            SendReply(arg, string.Format("Текущий fps сервера: {0}мс",Convert.ToString(Performance.current.frameRate)));
            fpsCheckCalled = true;
            fpsCaller = arg;
            fpsTime = 0.0;
            fpsCalled.Clear();
            timer.Once(2f, () => SendFPSCount());
        }
        private BasePlayer FindPlayer(string nameOrIdOrIp)
        {
            var player = BasePlayer.Find(nameOrIdOrIp);
            if (player == null)
            {
                ulong id;
                if (ulong.TryParse(nameOrIdOrIp, out id))
                    player = BasePlayer.FindSleeping(id);
            }
            return player;
        }
        
        void SendFPSCount()
        {
            if (fpsCaller is ConsoleSystem.Arg)
            {
            	fpsCheckCalled = false;
                SendReply((ConsoleSystem.Arg)fpsCaller, string.Format("Проверка всех игроков на сервере заняла {0}с", (fpsTime/1000.0).ToString()));
            }
        }
    }
}
