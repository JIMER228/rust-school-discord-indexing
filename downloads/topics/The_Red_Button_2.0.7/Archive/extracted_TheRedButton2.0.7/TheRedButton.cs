// Requires: SignArtist


using UnityEngine;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using System.Linq;
using System;
using Oxide.Core.Configuration;
using Oxide.Core;
using System.Collections;
using Oxide.Game.Rust.Cui;
using Pool = Facepunch.Pool;
using Physics = UnityEngine.Physics;
using Oxide.Game.Rust.Libraries;
using VLB;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Reflection;
using ConVar;


namespace Oxide.Plugins
{
    [Info("The Red Button", "NooBlet", "2.0.7")]
    [Description("Just a small plugin with a button to troll your players with A fun game . with some punishments on button press")]
    public class TheRedButton : RustPlugin
    {
        [PluginReference]
        private readonly Plugin SignArtist,Payback,Payback2,BombVest;

        #region Vars 

        private DynamicConfigFile Button_Data = Interface.Oxide.DataFileSystem.GetDatafile("Button_Data");
        private DynamicConfigFile PlayerButton_Data = Interface.Oxide.DataFileSystem.GetDatafile("PlayerButton_Data");
        private Dictionary<ulong,ButtonInfo> cachedButtons = new Dictionary<ulong, ButtonInfo>();
        private static List<string> cachedpunishmentcooldown = new List<string>();
        BTData btData;
        private static PluginConfig Settings;

        private Dictionary<ulong, DateTime> buttonPressCooldownData = new Dictionary<ulong, DateTime>();

        private const string MLRSRocketPrefab = "assets/content/vehicles/mlrs/rocket_mlrs.prefab";
        private const string scrapheli = "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab";
        private const string cactus = "assets/bundled/prefabs/autospawn/resource/v3_arid_cactus/cactus-2.prefab";
        private const string button = "assets/prefabs/deployable/playerioents/button/button.prefab";
        private const string sign = "assets/prefabs/deployable/signs/sign.post.double.prefab";
        private const string c4 = "assets/prefabs/tools/c4/explosive.timed.deployed.prefab";
        private const string landmine = "assets/prefabs/deployable/landmine/landmine.prefab";
        private const string scientist = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_roam.prefab";
        private const string scarecrow = "assets/prefabs/npc/scarecrow/scarecrow.prefab";
        private const string bear = "assets/rust.ai/agents/bear/bear.prefab";       
        private const string msign = "assets/prefabs/deployable/signs/sign.medium.wood.prefab";
        private const string LightPrefab = "assets/prefabs/deployable/playerioents/lights/sirenlight/electric.sirenlight.deployed.prefab";
        private const string toiletPrefab = "assets/bundled/prefabs/static/toilet_b.static.prefab";
        private const string effect = "assets/bundled/prefabs/fx/explosions/explosion_03.prefab";
        public const string sound_scream = "assets/bundled/prefabs/fx/player/beartrap_scream.prefab";
        public const string effect_onfire = "assets/bundled/prefabs/fx/player/onfire.prefab";
        public const string fireball = "assets/bundled/prefabs/static/campfire_static.prefab";
        MonumentInfo PVELordMonument = new MonumentInfo();
        MonumentInfo ScrapYard = new MonumentInfo();
        CargoShip cargo;
        private static TimedExplosive mlrsRocketTimedExplosive;
        private const ulong skinIDButton = 2893627061;
        private const string permUse = "TheRedButton.use";
        private const string permAdmin = "TheRedButton.admin";
        Signage SignBoard;
        MonumentInfo mon;
        private System.Random rand = new System.Random();
        private Dictionary<string, int> Punishments = new Dictionary<string, int>();
        private Vector3 SwimLocation = new Vector3(0,0,0);
        private string UIBGImage = "";
        Vector3 loc = new Vector3(0, 0, 0);
        private static int? _embedColor;      
        private string PunName;

        #endregion Vars      

        #region Hooks

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permAdmin, this);
            LoadConfig();
            LoadPuns();
            LoadData();
            var buttonlist = cachedButtons.ToList();
            foreach (var b in buttonlist)
            {
                var playerid = b.Value.OwningPlayer;
                if (playerid != 0)
                {
                    timer.Once(1f, () =>
                    {
                       
                        SpawnButton(playerid, b.Value.Location, b.Value.Rotation);
                        cachedButtons.Remove(b.Key);
                    });
                    
                }
                else { continue; }

            }
            SwimLocation = FindSwimLocation(SwimLocation);
            foreach(var p in BasePlayer.activePlayerList)
            {
                var e = p.gameObject.GetComponent<PlayerEffect>();
                if(e==null) continue;
                e.DestroyTimer();
                UnityEngine.Object.Destroy(e);
                DestroyPlayerComponent(p);
            }


        }     
      

        void Unload()
        {
            DestroySettingsUI();
            foreach (var p in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(p, "buttonmenu_panel");
                CuiHelper.DestroyUi(p, "selectedmenu_panel");
            }
            SaveData();
            foreach (var player in BasePlayer.activePlayerList)
            DestroyPlayerComponent(player);          
           
        }
        void OnNewSave(string filename)
        {
            cachedButtons.Clear();
            Button_Data.Clear();
            Button_Data.Save();
            PlayerButton_Data.Clear();
            PlayerButton_Data.Clear();
        }

        void OnEntitySpawned(CargoShip ship)
        {
            cargo = ship;
        }
        void OnEntityKill(CargoShip ship)
        {
            cargo = null;
        }
        //object OnItemPickup(Item item, BasePlayer player)
        //{
        //    if(player == null) { return null; }
        //    if(item.GetWorldEntity()._name == "buttonpoop")
        //    {
        //        item.RemoveFromContainer();
        //        return false;
        //    }
        //    return null;
        //}

        private object OnPlayerViolation(BasePlayer player, AntiHackType type)
        {
            if (type == AntiHackType.FlyHack && player._name =="flyhackstop") return false;
            return null;
        }
        private void OnEntityBuilt(Planner plan, GameObject go)
        {

            if (go == null) { return; }

            RunChecks(plan.GetOwnerPlayer(), go.ToBaseEntity());
        }
        object CanPickupEntity(BasePlayer player, BaseEntity entity)
        {
            if(entity._name == "0304") { return false; }
            return null;
        }
        private void RunChecks(BasePlayer player,BaseEntity baseEntity)
        {
            CheckDeployButton(player,baseEntity);

        }
        
       
        object OnEntityTakeDamage(Signage entity, HitInfo info)
        {
            if (entity == null) { return null; }
            if (info == null) { return null; }

            if (entity._name == "0304") { return false; }
            return null;
        }
        object OnEntityTakeDamage(PressButton entity, HitInfo info)
        {
            if (entity == null) { return null; }
            if (info == null) { return null; }

            if (entity._name == "0304") { return false; }
            return null;
        }
        object OnEntityTakeDamage(SirenLight entity, HitInfo info)
        {
            if (entity == null) { return null; }
            if (info == null) { return null; }

            if (entity._name == "0304") { return false; }
            
            return null;
        }

       

        void OnEntityKill(Signage entity)
        {
            KillButtons(entity);
        }

      

        object OnMessagePlayer(string message, BasePlayer player)
        {
            if (message.Contains("succesfully loaded to the sign!")) return false;
            return null;
        }
        void OnPlayerSleep(BasePlayer player)
        {
            int count = 0;
            var playerInventoryAllItems = new List<Item>();
            player.inventory.GetAllItems(playerInventoryAllItems);
            foreach (var i in playerInventoryAllItems)
            {
                if (i.skin == skinIDButton)
                {
                    PlayerButton_Data[player.UserIDString] = count+ i.amount;
                    i.Remove();
                }
                PlayerButton_Data.Save();
            }
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {           
            if (PlayerButton_Data[player.UserIDString] != null)
            {
                
                for (int i = 0; i < Convert.ToInt32(PlayerButton_Data[player.UserIDString]); i++)
                {
                    var button = ItemManager.CreateByName("sign.post.double", 1);
                    button.name = "The Red Button";
                    button.skin = skinIDButton;
                    player.GiveItem(button);
                }
            }
            PlayerButton_Data.Remove(player.UserIDString);
            PlayerButton_Data.Save();

            foreach (var b in cachedButtons)
            {
                if(b.Value.OwningPlayer == player.userID)
                {
                    NetworkableId buttonid = new NetworkableId(ulong.Parse(b.Key.ToString()));
                    
                    var board = Signage.serverEntities.Find(buttonid) as Signage;
                    if(board == null) { continue; }
                    SignArtist.CallHook("API_SkinSign", player, board, Settings.buttonSettings.PictureURL);
                }
            }

        }
        object OnButtonPress(PressButton button, BasePlayer player)
        {           
            if (button._name == "0304")
            {
                if (CheckisCooldown(player.userID)) { player.ChatMessage(GetLang("ButtonCooldownMessage", player)); return false; }
                var punishment = RandomPunishment(player, button.transform.position);
                var msg = $"<color=green><size={Settings.buttonSettings.broadcastFontSize}>{player.displayName} Pressed The </color><color=red>RED</color><color=green> Button and {punishment}</color></size>";
                Interface.CallHook("RedButtonPressed", player,punishment);
                if (Settings.buttonSettings.broadcastButtonAction)
                {
                    Server.Broadcast(msg);
                }
                if (!buttonPressCooldownData.ContainsKey(player.userID)) { buttonPressCooldownData.Add(player.userID, DateTime.Now.AddMinutes(Settings.buttonSettings.buttonCooldown)); } else { buttonPressCooldownData[player.userID] = DateTime.Now.AddMinutes(Settings.buttonSettings.buttonCooldown); }
                
            }

            return null;
        }

       
        void RedButtonPressed(BasePlayer player,string punishment)
        {
            
            if (punishment.Contains("custom"))
            {
                _embedColor = FromHex("#1fa735");
            }
            else
            {
                _embedColor = FromHex("#8f212e");
            }
            if (Settings.buttonSettings.sendDiscord)
            {
                //uint crc = r.mugshotCrc;
                //var encodedPng = FileStorage.server.Get(crc, FileStorage.Type.png, player.net.ID);
                //if (encodedPng == null) return;
                timer.Once(0.2f, () =>
                {
                    SendDiscordEmbed(/*encodedPng,*/ player, punishment);
                });
                
            }
        }

        private void DestroyPlayerComponent(BasePlayer player)
        {
            PlayerEffect playerEffect = GetPlayer(player);
            if (playerEffect == null)
                return;

            UnityEngine.Object.Destroy(playerEffect);
        }
        private PlayerEffect GetPlayer(BasePlayer player)
        {
            if (player == null)
                return null;

            var playerEffect = player.GetComponent<PlayerEffect>();
            if (playerEffect == null)
                return null;

            return playerEffect;
        }

        #endregion Hooks

        #region Core

        private void FindOldButtonsAndKill(Vector3 vec)
        {
           foreach(var b in PressButton.serverEntities)
            {
                if(b!=null && b is PressButton)
                {
                   var button = b as PressButton;   
                    if(button.GetEntity()._name == "0304")
                    {
                        button.Kill();
                    }
                }
            }
        }

        private static void KillButtons(Signage entity)
        {
            if (entity._name == "0304")
            {
                var buttons = Pool.Get<List<PressButton>>();
                Vis.Entities(entity.transform.position, 0.5f, buttons);

                foreach (var b in buttons)
                {
                    if (b.OwnerID == entity.OwnerID)
                    {
                        b.Kill();
                    }
                }
                Pool.FreeUnmanaged(ref buttons);
            }
        }

        private void Teleport(BasePlayer player, Vector3 target)
        {            
            float currenthealth = player.health;
            string tp = player._name;
            player._name = "flyhackstop";
            player.health = 10000f;
            player.Teleport(target);
            timer.Once(20f, () =>
            {
                player._name = tp;
                player.health = currenthealth;
            });

        }
        private string getTimeRemain(BasePlayer player)
        {
            if (!buttonPressCooldownData.ContainsKey(player.userID)) 
            {
                return "00:00";
            }
            TimeSpan ts = buttonPressCooldownData[player.userID] - DateTime.Now;          

            var time = $"{ts:mm}:{ts:ss}";
           
            return time;
        }
        private bool CheckisCooldown(ulong userID)
        {
            if (!buttonPressCooldownData.ContainsKey(userID)) { return false; }
            if (buttonPressCooldownData[userID] > DateTime.Now) {  return true; }            
            return false;
        }

        private void CheckDeployButton(BasePlayer player,BaseEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            if (!IsButton(entity.skinID))
            {
                return;
            }

            var transform = entity.transform;


            SpawnButton(player.userID,transform.position, transform.rotation, 0304);
            entity.transform.position -= new Vector3(0, 3, 0);
            entity.SendNetworkUpdate();

            NextTick(() => { entity?.Kill(); });
        }
        private bool IsButton(ulong skin)
        {
            return skin == skinIDButton;
        }
        private void SpawnButton(ulong playerid, Vector3 position, Quaternion rotation = default(Quaternion), ulong ownerID = 0,bool light=true)
        {
            var signpost = GameManager.server.CreateEntity(sign, position, rotation);
          
            if (signpost == null) return;
            var sp = signpost?.GetComponent<BaseCombatEntity>();
            sp._maxHealth = 99999999f;
            sp._health = 99999999f;
            signpost.OwnerID = playerid; 
            signpost.GetEntity()._name = "0304";            
            signpost.Spawn();        



            var signboard = GameManager.server.CreateEntity(msign) as Signage;
            if (signboard == null) return;
            signboard.SetParent(signpost);
            signboard.pickup.enabled = false;
            signboard.transform.localPosition = new Vector3(0f, 1.57f, 0.03f);
            RemoveColliderProtection(signboard);
            signboard._maxHealth = 99999999f;
            signboard._health = 99999999f;
            signboard.OwnerID = playerid;
            signboard.GetEntity()._name = "0304";            
            signboard.Spawn();
            SignBoard = signboard;
            var player = findPlayer(playerid.ToString());
            if(player!= null)
            {
               SignArtist.CallHook("API_SkinSign", player, signboard, Settings.buttonSettings.PictureURL);
            }
            
            signboard.SetFlag(Signage.Flags.Locked, true);




            var signbutton = GameManager.server.CreateEntity(button,position,rotation) as PressButton;
            if (signbutton == null) return;
          
            signbutton.pickup.enabled = false;
            var correction = new Vector3(0f, 1.4f, 0.01f);
            signbutton.transform.position = position + signpost.transform.rotation * correction;

            signbutton.OwnerID = playerid;
            signbutton.GetEntity()._name = "0304";
            RemoveColliderProtection(signbutton);
            signbutton._maxHealth = 99999999f;
            signbutton._health = 99999999f;
            signbutton.Spawn();
            signbutton.SendNetworkUpdateImmediate();

            if (light)
            {
                SirenLight backLights =
                         GameManager.server.CreateEntity(LightPrefab) as SirenLight;
                if (backLights == null) return;
                backLights._maxHealth = 99999999f;
                backLights._health = 99999999f;
                backLights.OwnerID = playerid;
                backLights.GetEntity()._name = "0304";
                RemoveColliderProtection(backLights);
                backLights.SetFlag(BaseEntity.Flags.On, true);
                backLights.SetParent(signpost);
                backLights.transform.localPosition = new Vector3(0, 2.6f, 0f);
                backLights.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, 0));
                backLights.Spawn();
                backLights.UpdateHasPower(10, 1);


                backLights.SendNetworkUpdateImmediate();

            }
            SaveButton(signpost,playerid,signboard);
           
        }

        string GetPunishment()
        {
            string Result = string.Empty;
            var ValidPunishments = (from Punishment in Punishments
                                    join cachedpunishment in cachedpunishmentcooldown on Punishment.Key equals cachedpunishment
                                    into PunJoin
                                    from PJ in PunJoin.DefaultIfEmpty()
                                    where PJ == null
                                    select Punishment).ToDictionary(x => x.Key, x => x.Value);
            if (ValidPunishments == null || ValidPunishments.Count == 0)
            {
                ValidPunishments = Punishments;
                cachedpunishmentcooldown.Clear();
            }

            int nRand = rand.Next(Int32.MaxValue);
            int Total = ValidPunishments.Sum(x => x.Value);
            nRand = nRand % Total;
            int nCurrent = 0;

            foreach (var KVP in ValidPunishments.Where(x => x.Value > 0).OrderBy(x => x.Value))
            {
                int nNext = nCurrent + KVP.Value;
                if (nRand >= nCurrent && nRand < nNext)
                {
                    StartPunishcooldownLogic(KVP.Key);
                    return KVP.Key;
                }
                    
                nCurrent = nNext;
            }
            
            return Result;
        }


        private void StartPunishcooldownLogic(string result)
        {
            cachedpunishmentcooldown.Add(result);
            Puts($"added {result} to cooldown");
            timer.Once(60f, () =>
            {
                if(cachedpunishmentcooldown.Contains(result))
                {
                    cachedpunishmentcooldown.Remove(result);
                    Puts($"removed {result} to cooldown");
                }              
            });

        }

        private static BaseEntity GetLookEntity(BasePlayer basePlayer, float maxDistance = 20)
        {
            RaycastHit hit;
            return Physics.Raycast(basePlayer.eyes.HeadRay(), out hit, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.GetEntity()
                : null;
        }
       

        string RandomPunishment(BasePlayer player,Vector3 startloc)
        {
            string pun = "";    
          
            var random = GetPunishment();
            PunName = random;
            Puts($"Red Button Punish {player.displayName} with : {random}");
            Vector3 loc;
            switch (random)
            {
                case "guntrap":
                    int raycasts = Mathf.CeilToInt(360 / 5 * 0.1375f);
                    var PlayerPositions = GetCircumferencePositions(player.transform.position, 4, raycasts, 0f, true);
                    int nUsed = 0;
                    foreach (var p in PlayerPositions)
                    {
                        Vector3 position = PlayerPositions.Skip(nUsed).First();
                        nUsed++;
                        Vector3 relativePos = player.transform.position - position;
                        Quaternion rotation = Quaternion.LookRotation(relativePos, Vector3.up);
                        var trap = GameManager.server.CreateEntity("assets/prefabs/deployable/single shot trap/guntrap.deployed.prefab", position, rotation);
                        trap.Spawn();
                        var container = trap.GetComponent<StorageContainer>();
                        var item = ItemManager.CreateByItemID(588596902, 10);
                        if (item == null) { return ""; }
                        timer.Once(0.4f, () =>
                        {
                            item.MoveToContainer(container.inventory);
                        });

                        timer.Once(4f, () =>
                        {
                            if (Vector3.Distance(player.transform.position, position) < 10 && player.IsAlive()) { player.Die(); }
                            if (trap) { trap.Kill(); }
                        });
                    }
                    return GetLang("GuntrapMessage", player);

                case "bombvest":
                       BombVest.CallHook("CuffPlayer", player);
                    return GetLang("BombVestMessage", player);

                case "explosivediarrhea":
                    StartDiaria(player);
                    return GetLang("DiarrheaMessage", player);

                case "fireball":
                    Fireball(player);
                    return GetLang("FireBallMessage", player);

                case "shredder":
                    if (GetJunkYardMonument())
                    {
                        var correct = new Vector3(10f, 20f, 5f);
                        var transform = ScrapYard.transform;
                        var rot = transform.rotation;
                        loc = transform.position + rot * correct;
                        Teleport(player, loc);
                        return GetLang("ShredderMessage", player);
                    }
                    return "";
                   

                case "pvelord":
                    if (cargo != null)
                    {
                        Puts("cargo is out");
                        var correct = new Vector3(1f, 2f, 1f);
                        var transform = cargo.transform;
                        var rot = transform.rotation;
                        loc = transform.position + rot * correct;
                        Teleport(player, loc);
                        return GetLang("PVELordMessageCargo", player);
                    } 
                    if (!GetPVEMonument()) { player.Hurt(190f);return "Died"; }
                    if (PVELordMonument.name == "OilrigAI2")                  
                    {
                        var correct = new Vector3(-8f, 38f, 9f);                       

                        var transform = PVELordMonument.transform;
                        var rot = transform.rotation;
                        loc = transform.position + rot * correct;
                        SendPlayerToPVE(player,loc);
                        return GetLang("PVELordMessageLargeOil", player);
                    }
                    if (PVELordMonument.name == "OilrigAI")
                    {
                        var correct = new Vector3(14f, 29f, -14f);

                        var transform = PVELordMonument.transform;
                        var rot = transform.rotation;
                        loc = transform.position + rot * correct;
                        SendPlayerToPVE(player, loc);
                        return GetLang("PVELordMessageSmallOil", player);
                    }
                    return "";

                case "landmine":
                    var l = GameManager.server.CreateEntity(landmine, player.transform.position) as Landmine;
                    if (l == null) return null;
                    var l2 = GameManager.server.CreateEntity(landmine, player.transform.position) as Landmine;
                    if (l2 == null) return null;
                    l._name = "buttonmine";
                    l.Spawn();
                    l.Arm();
                    l.Explode();
                    l2._name = "buttonmine";
                    l2.Spawn();
                    l2.Arm();  
                    l2.Explode();
                    player?.Die();
                    return GetLang("LandmineMessage", player);                  
               
                case "slap":
                    player.Hurt(200f);
                   
                    string[] effects = new[] // TODO: Move to configuration
                    {
                       "headshot",
                       "headshot_2d",
                       "impacts/slash/clothflesh/clothflesh1",
                       "impacts/stab/clothflesh/clothflesh1"
                    };
                    string effect = effects.GetRandom();
                    Effect.server.Run($"assets/bundled/prefabs/fx/{effect}.prefab",player.transform.position, UnityEngine.Vector3.zero);
                    if (!player.IsDead())
                    {
                        player?.Die();
                    }
                    return GetLang("SlapMessage", player);

                case "mlrs":
                    timer.Repeat(0.6f, 3, () =>
                    {
                        ExecuteFireOperation(player, player.transform.position);
                    });
                    return GetLang("MlrsMessage", player);

                case "scrapheli":
                    Vector3 finalpos = GetPosFromButton(player, startloc,10);
                    player.Teleport(finalpos);
                    //loc = new Vector3(player.transform.position.x+6,player.transform.position.y + 5,player.transform.position.z+6);
                    //player.Teleport(loc);
                    var loc2 = new Vector3(finalpos.x, finalpos.y + 3, finalpos.z);
                    var heli = GameManager.server.CreateEntity(scrapheli, loc2);
                    timer.Once(0.5f, () =>
                    {
                        heli.Spawn();
                        timer.Once(10f, () =>
                        {
                            heli.Kill();
                        });
                    });
                    return GetLang("ScrapheliMessage", player);

                case "cactus":
                    player.SetHealth(0.5f);
                    var cac = GameManager.server.CreateEntity(cactus,player.transform.position);
                    cac.Spawn();                   
                    timer.Once(10f, () =>
                    {
                        cac.Kill();
                    });
                    return GetLang("CactusMessage", player);

                case "fall":
                    loc = new Vector3(player.transform.position.x, player.transform.position.y + 50, player.transform.position.z);
                    string tp = player._name;
                    player._name = "flyhackstop";                    
                    player.Teleport(loc);
                    timer.Once(10f, () =>
                    {
                        player._name = tp;                       
                    });
                    return GetLang("FallMessage", player);

                case "swim":
                    if (Settings.buttonSettings.locSwimRandom)
                    {
                        loc = TerrainMeta.RandomPointOffshore(); 
                    }
                    else
                    {
                        loc = SetupLabPos();
                    }

                    player.Teleport(loc);
                    SimpleShark shark;
                    string sharkPrefab = "assets/rust.ai/agents/fish/simpleshark.prefab";

                    BaseEntity entity = GameManager.server.CreateEntity(sharkPrefab, player.transform.position + new Vector3(-2, -2, -2));
                    BaseEntity entity2 = GameManager.server.CreateEntity(sharkPrefab, player.transform.position + new Vector3(-2, -2, -2));
                    BaseEntity entity3 = GameManager.server.CreateEntity(sharkPrefab, player.transform.position + new Vector3(-2, -2, -2));

                    shark = entity as SimpleShark;
                    entity.Spawn();
                    entity2.Spawn();
                    entity3.Spawn();
                    timer.Once(60f, () =>
                    {
                        if (entity)
                        {
                            entity.Kill();
                        }
                        if (entity2)
                        {
                            entity2.Kill();
                        }
                        if (entity3)
                        {
                            entity3.Kill();
                        }
                    });

                    return GetLang("SwimMessage", player);

                case "bear":
                    loc = new Vector3(player.transform.position.x + 1, player.transform.position.y, player.transform.position.z + 1);
                    var b = GameManager.server.CreateEntity(bear, loc, default(Quaternion)) as Bear;
                    var b2 = GameManager.server.CreateEntity(bear, loc, default(Quaternion)) as Bear;
                    var b3 = GameManager.server.CreateEntity(bear, loc, default(Quaternion)) as Bear;
                    if (b == null) return null;
                    if (b2 == null) return null;
                    if (b3 == null) return null;
                    b.Spawn();
                    b2.Spawn();
                    b3.Spawn();
                    b.StartAttacking(player);
                    b2.StartAttacking(player);
                    b3.StartAttacking(player);

                    timer.Once(20f, () =>
                    {
                        b3.Kill();
                        b2.Kill();
                        b.Kill();
                    });
                    return GetLang("BearMessage", player);

                case "scientist":
                    loc = new Vector3(player.transform.position.x + 2, player.transform.position.y, player.transform.position.z + 2);
                    var s = GameManager.server.CreateEntity(scientist, loc);
                   
                    if (s == null) return null;
                  
                    s.Spawn();
                 
                    
                    timer.Once(20f, () =>
                    {
                        s.Kill();
                       
                    });
                    return GetLang("ScientistMessage", player);

                case "c4":
                    var c = GameManager.server.CreateEntity(c4, player.transform.position) as TimedExplosive;
                    if (c == null) return null;
                    c.SetParent(player);
                    RemoveColliderProtection(c);
                    c.transform.localPosition = new Vector3(0f, 1f, 0.1f);
                    c._name = "0304";
                    c.Spawn();
                    c.SetCollisionEnabled(false);
                    c.SetMotionEnabled(false);
                  
                    
                    if (!Settings.buttonSettings.c4Timed)
                    {
                        c.SetFuse(0.1f);
                    }
                    return GetLang("C4Message", player);

                case "radiation":
                    player.metabolism.radiation_poison.Add(500f);
                    player.UpdateRadiation(500f);
                    player.SetHealth(5f);
                    return GetLang("RadiationMessage", player);

                case "scarecrow":                   
                        loc = new Vector3(player.transform.position.x + 2, player.transform.position.y, player.transform.position.z + 2);
                        var sc = GameManager.server.CreateEntity(scarecrow, loc);
                        var sc2 = GameManager.server.CreateEntity(scarecrow, loc);
                        var sc3 = GameManager.server.CreateEntity(scarecrow, loc);
                        if (sc == null) return null;
                        if (sc2 == null) return null;
                        if (sc3 == null) return null;
                        sc.Spawn();
                        sc2.Spawn();
                        sc3.Spawn();
                        timer.Once(20f, () =>
                        {
                            sc.Kill();
                            sc2.Kill();
                            sc3.Kill();
                        });
                       
                        return GetLang("ScarecrowMessage", player);



                case "customcommand1":
                    string playeroffset = $"{player.transform.position.x + 2},{player.transform.position.y},{player.transform.position.z+1}" ;
                    string consolecommand = Settings.customCommands.CustomCommand1.Command
                        .Replace("{playername}", player.displayName)
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{playerlocation}", playeroffset);
                                        
                    
                     Server.Command(consolecommand);
                   
                    return Settings.customCommands.CustomCommand1.Message;

                case "customcommand2":
                    string playeroffset2 = $"{player.transform.position.x + 2},{player.transform.position.y},{player.transform.position.z + 1}";
                    string consolecommand2 = Settings.customCommands.CustomCommand2.Command
                        .Replace("{playername}", player.displayName)
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{playerlocation}", playeroffset2);


                    Server.Command(consolecommand2);

                    return Settings.customCommands.CustomCommand2.Message;

                case "customcommand3":
                    string playeroffset3 = $"{player.transform.position.x + 2},{player.transform.position.y},{player.transform.position.z + 1}";
                    string consolecommand3 = Settings.customCommands.CustomCommand3.Command
                        .Replace("{playername}", player.displayName)
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{playerlocation}", playeroffset3);


                    Server.Command(consolecommand3);

                    return Settings.customCommands.CustomCommand3.Message;

                case "customcommand4":
                    string playeroffset4 = $"{player.transform.position.x + 2},{player.transform.position.y},{player.transform.position.z + 1}";
                    string consolecommand4 = Settings.customCommands.CustomCommand4.Command
                        .Replace("{playername}", player.displayName)
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{playerlocation}", playeroffset4);


                    Server.Command(consolecommand4);

                    return Settings.customCommands.CustomCommand4.Message;

                case "customcommand5":
                    string playeroffset5 = $"{player.transform.position.x + 2},{player.transform.position.y},{player.transform.position.z + 1}";
                    string consolecommand5 = Settings.customCommands.CustomCommand5.Command
                        .Replace("{playername}", player.displayName)
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{playerlocation}", playeroffset5);


                    Server.Command(consolecommand5);

                    return Settings.customCommands.CustomCommand5.Message;

                case "customcommand6":
                    string playeroffset6 = $"{player.transform.position.x + 2},{player.transform.position.y},{player.transform.position.z + 1}";
                    string consolecommand6 = Settings.customCommands.CustomCommand6.Command
                        .Replace("{playername}", player.displayName)
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{playerlocation}", playeroffset6);


                    Server.Command(consolecommand6);

                    return Settings.customCommands.CustomCommand6.Message;

                case "customcommand7":
                    string playeroffset7 = $"{player.transform.position.x + 2},{player.transform.position.y},{player.transform.position.z + 1}";
                    string consolecommand7 = Settings.customCommands.CustomCommand7.Command
                        .Replace("{playername}", player.displayName)
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{playerlocation}", playeroffset7);


                    Server.Command(consolecommand7);

                    return Settings.customCommands.CustomCommand7.Message;

                case "customcommand8":
                    string playeroffset8 = $"{player.transform.position.x + 2},{player.transform.position.y},{player.transform.position.z + 1}";
                    string consolecommand8 = Settings.customCommands.CustomCommand8.Command
                        .Replace("{playername}", player.displayName)
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{playerlocation}", playeroffset8);


                    Server.Command(consolecommand8);

                    return Settings.customCommands.CustomCommand8.Message;

                case "customcommand9":
                    string playeroffset9 = $"{player.transform.position.x + 2},{player.transform.position.y},{player.transform.position.z + 1}";
                    string consolecommand9= Settings.customCommands.CustomCommand9.Command
                        .Replace("{playername}", player.displayName)
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{playerlocation}", playeroffset9);


                    Server.Command(consolecommand9);

                    return Settings.customCommands.CustomCommand9.Message;

                case "customcommandA":
                    string playeroffsetA = $"{player.transform.position.x + 2},{player.transform.position.y},{player.transform.position.z + 1}";
                    string consolecommandA = Settings.customCommands.CustomCommandA.Command
                        .Replace("{playername}", player.displayName)
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{playerlocation}", playeroffsetA);


                    Server.Command(consolecommandA);

                    return Settings.customCommands.CustomCommandA.Message;

                // Payback Commands

                case "rocketman":
                    string rocketmancommand = "rm {playerid}"
                        .Replace("{playerid}", player.UserIDString);  
                    Server.Command(rocketmancommand);
                    return GetLang("Payback:RocketManMessage", player);

                case "shark":
                    string sharkcommand = "jaws {playerid}"
                        .Replace("{playerid}", player.UserIDString);    
                    Server.Command(sharkcommand);
                    return GetLang("Payback:SharkMessage", player);

                case "shocker":
                    player.SetHealth(10f);
                    string shockercommand = "sh {playerid}"
                        .Replace("{playerid}", player.UserIDString);
                    Server.Command(shockercommand);
                    timer.Once(5f, () =>
                    {
                        Server.Command(shockercommand);
                    });
                    return GetLang("Payback:ShockerMessage", player);

                case "naked":
                    string nakedcommand = "nk {playerid}"
                        .Replace("{playerid}", player.UserIDString);                     
                    Server.Command(nakedcommand);
                    return GetLang("Payback:NakedMessage", player);

                case "chickens":
                    string[] args = {
                        "",
                    };
                    Vector3 pos = GetPosFromButton(player, startloc, 3);
                    Payback.Call("SpawnChickens", player, pos, args);
                    return GetLang("Payback:ChickensMessage", player);

                //Payback2 commands
                case "train":

                    PlayerLookDown(player);
                    BlockMovement(player);
                    Payback2.Call("DoTrain", player, player, null);
                    return GetLang("Payback2:TrainMessage", player);

                case "f15":

                    string[] args2 = {
                        "",
                    };
                    Payback2.Call("DoAirstrike", player, player, args2);
                    return GetLang("Payback2:F15Message", player);

            }
            return pun;
        }

        string DoPunishment(BasePlayer player, Vector3 startloc,string random)
        {
            int playertotargerdis = (int)Vector3.Distance(startloc,player.transform.position);
            string pun = "";
            Puts($"Red Button Punish {player.displayName} with : {random}");
            Vector3 loc;
            switch (random)
            {
                case "guntrap":
                    int raycasts = Mathf.CeilToInt(360 / 5 * 0.1375f);
                    var PlayerPositions = GetCircumferencePositions(player.transform.position, 4, raycasts, 0f, true);
                    int nUsed = 0;
                    foreach (var p in PlayerPositions)
                    {
                        Vector3 position = PlayerPositions.Skip(nUsed).First();
                        nUsed++;
                        Vector3 relativePos = player.transform.position - position;
                        Quaternion rotation = Quaternion.LookRotation(relativePos, Vector3.up);
                        var trap = GameManager.server.CreateEntity("assets/prefabs/deployable/single shot trap/guntrap.deployed.prefab", position, rotation);
                        trap.Spawn();
                        var container = trap.GetComponent<StorageContainer>();
                        var item = ItemManager.CreateByItemID(588596902, 10);
                        if (item == null) { return ""; }
                        timer.Once(0.4f, () =>
                        {
                            item.MoveToContainer(container.inventory);
                        });

                        timer.Once(4f, () =>
                        {
                            if (Vector3.Distance(player.transform.position, position) < 10 && player.IsAlive()) { player.Die(); }
                            if (trap) { trap.Kill(); }
                        });
                    }
                    return GetLang("GuntrapMessage", player);

                case "bombvest":
                    BombVest.CallHook("CuffPlayer", player);
                    return GetLang("BombVestMessage", player);

                case "explosivediarrhea":
                    StartDiaria(player);
                    return GetLang("DiarrheaMessage", player);

                case "fireball":
                    Fireball(player);
                    return GetLang("FireBallMessage", player);

                case "shredder":
                    if (GetJunkYardMonument())
                    {
                        var correct = new Vector3(10f, 20f, 5f);
                        var transform = ScrapYard.transform;
                        var rot = transform.rotation;
                        loc = transform.position + rot * correct;
                        Teleport(player, loc);
                        return GetLang("ShredderMessage", player);
                    }
                    return "";


                case "pvelord":
                    if (cargo != null)
                    {
                        Puts("cargo is out");
                        var correct = new Vector3(1f, 2f, 1f);
                        var transform = cargo.transform;
                        var rot = transform.rotation;
                        loc = transform.position + rot * correct;
                        Teleport(player, loc);
                        return GetLang("PVELordMessageCargo", player);
                    }
                    if (!GetPVEMonument()) { player.Hurt(190f); return "Died"; }
                    if (PVELordMonument.name == "OilrigAI2")
                    {
                        var correct = new Vector3(-8f, 38f, 9f);

                        var transform = PVELordMonument.transform;
                        var rot = transform.rotation;
                        loc = transform.position + rot * correct;
                        SendPlayerToPVE(player, loc);
                        return GetLang("PVELordMessageLargeOil", player);
                    }
                    if (PVELordMonument.name == "OilrigAI")
                    {
                        var correct = new Vector3(14f, 29f, -14f);

                        var transform = PVELordMonument.transform;
                        var rot = transform.rotation;
                        loc = transform.position + rot * correct;
                        SendPlayerToPVE(player, loc);
                        return GetLang("PVELordMessageSmallOil", player);
                    }
                    return "";

                case "landmine":
                    var l = GameManager.server.CreateEntity(landmine, player.transform.position) as Landmine;
                    if (l == null) return null;
                    var l2 = GameManager.server.CreateEntity(landmine, player.transform.position) as Landmine;
                    if (l2 == null) return null;
                    l._name = "buttonmine";
                    l.Spawn();
                    l.Arm();
                    l.Explode();
                    l2._name = "buttonmine";
                    l2.Spawn();
                    l2.Arm();
                    l2.Explode();
                    player?.Die();
                    return GetLang("LandmineMessage", player);

                case "slap":
                    player.Hurt(200f);

                    string[] effects = new[] // TODO: Move to configuration
                    {
                       "headshot",
                       "headshot_2d",
                       "impacts/slash/clothflesh/clothflesh1",
                       "impacts/stab/clothflesh/clothflesh1"
                    };
                    string effect = effects.GetRandom();
                    Effect.server.Run($"assets/bundled/prefabs/fx/{effect}.prefab", player.transform.position, UnityEngine.Vector3.zero);
                    if (!player.IsDead())
                    {
                        player?.Die();
                    }
                    return GetLang("SlapMessage", player);

                case "mlrs":
                    timer.Repeat(0.6f, 3, () =>
                    {
                        ExecuteFireOperation(player, player.transform.position);
                    });
                    return GetLang("MlrsMessage", player);

                case "scrapheli":
                    Vector3 finalpos = GetPosFromButton(player, startloc, 10);
                    player.Teleport(finalpos);
                    //loc = new Vector3(player.transform.position.x+6,player.transform.position.y + 5,player.transform.position.z+6);
                    //player.Teleport(loc);
                    var loc2 = new Vector3(finalpos.x, finalpos.y + 3, finalpos.z);
                    var heli = GameManager.server.CreateEntity(scrapheli, loc2);
                    timer.Once(0.5f, () =>
                    {
                        heli.Spawn();
                        timer.Once(10f, () =>
                        {
                            heli.Kill();
                        });
                    });
                    return GetLang("ScrapheliMessage", player);

                case "cactus":
                    player.SetHealth(0.5f);
                    var cac = GameManager.server.CreateEntity(cactus, player.transform.position);
                    cac.Spawn();
                    timer.Once(10f, () =>
                    {
                        cac.Kill();
                    });
                    return GetLang("CactusMessage", player);

                case "fall":
                    loc = new Vector3(player.transform.position.x, player.transform.position.y + 50, player.transform.position.z);
                    string tp = player._name;
                    player._name = "flyhackstop";
                    player.Teleport(loc);
                    timer.Once(10f, () =>
                    {
                        player._name = tp;
                    });
                    return GetLang("FallMessage", player);

                case "swim":
                    if (Settings.buttonSettings.locSwimRandom)
                    {
                        loc = TerrainMeta.RandomPointOffshore();
                    }
                    else
                    {
                        loc = SetupLabPos();
                    }

                    player.Teleport(loc);
                    SimpleShark shark;
                    string sharkPrefab = "assets/rust.ai/agents/fish/simpleshark.prefab";

                    BaseEntity entity = GameManager.server.CreateEntity(sharkPrefab, player.transform.position + new Vector3(-2, -2, -2));
                    BaseEntity entity2 = GameManager.server.CreateEntity(sharkPrefab, player.transform.position + new Vector3(-2, -2, -2));
                    BaseEntity entity3 = GameManager.server.CreateEntity(sharkPrefab, player.transform.position + new Vector3(-2, -2, -2));

                    shark = entity as SimpleShark;
                    entity.Spawn();
                    entity2.Spawn();
                    entity3.Spawn();
                    timer.Once(60f, () =>
                    {
                        if (entity)
                        {
                            entity.Kill();
                        }
                        if (entity2)
                        {
                            entity2.Kill();
                        }
                        if (entity3)
                        {
                            entity3.Kill();
                        }
                    });

                    return GetLang("SwimMessage", player);

                case "bear":
                    loc = new Vector3(player.transform.position.x + 1, player.transform.position.y, player.transform.position.z + 1);
                    var b = GameManager.server.CreateEntity(bear, loc, default(Quaternion)) as Bear;
                    var b2 = GameManager.server.CreateEntity(bear, loc, default(Quaternion)) as Bear;
                    var b3 = GameManager.server.CreateEntity(bear, loc, default(Quaternion)) as Bear;
                    if (b == null) return null;
                    if (b2 == null) return null;
                    if (b3 == null) return null;
                    b.Spawn();
                    b2.Spawn();
                    b3.Spawn();
                    b.StartAttacking(player);
                    b2.StartAttacking(player);
                    b3.StartAttacking(player);

                    timer.Once(20f, () =>
                    {
                        b3.Kill();
                        b2.Kill();
                        b.Kill();
                    });
                    return GetLang("BearMessage", player);

                case "scientist":
                    loc = new Vector3(player.transform.position.x + 2, player.transform.position.y, player.transform.position.z + 2);
                    var s = GameManager.server.CreateEntity(scientist, loc);

                    if (s == null) return null;

                    s.Spawn();


                    timer.Once(20f, () =>
                    {
                        s.Kill();

                    });
                    return GetLang("ScientistMessage", player);

                case "c4":
                    var c = GameManager.server.CreateEntity(c4, player.transform.position) as TimedExplosive;
                    if (c == null) return null;
                    c.SetParent(player);
                    RemoveColliderProtection(c);
                    c.transform.localPosition = new Vector3(0f, 1f, 0.1f);
                    c._name = "0304";
                    c.Spawn();
                    c.SetCollisionEnabled(false);
                    c.SetMotionEnabled(false);


                    if (!Settings.buttonSettings.c4Timed)
                    {
                        c.SetFuse(0.1f);
                    }
                    return GetLang("C4Message", player);

                case "radiation":
                    player.metabolism.radiation_poison.Add(500f);
                    player.UpdateRadiation(500f);
                    player.SetHealth(5f);
                    return GetLang("RadiationMessage", player);

                case "scarecrow":
                    loc = new Vector3(player.transform.position.x + 2, player.transform.position.y, player.transform.position.z + 2);
                    var sc = GameManager.server.CreateEntity(scarecrow, loc);
                    var sc2 = GameManager.server.CreateEntity(scarecrow, loc);
                    var sc3 = GameManager.server.CreateEntity(scarecrow, loc);
                    if (sc == null) return null;
                    if (sc2 == null) return null;
                    if (sc3 == null) return null;
                    sc.Spawn();
                    sc2.Spawn();
                    sc3.Spawn();
                    timer.Once(20f, () =>
                    {
                        sc.Kill();
                        sc2.Kill();
                        sc3.Kill();
                    });

                    return GetLang("ScarecrowMessage", player);



                case "customcommand1":
                    string playeroffset = $"{player.transform.position.x + 2},{player.transform.position.y},{player.transform.position.z + 1}";
                    string consolecommand = Settings.customCommands.CustomCommand1.Command
                        .Replace("{playername}", player.displayName)
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{playerlocation}", playeroffset);


                    Server.Command(consolecommand);

                    return Settings.customCommands.CustomCommand1.Message;

                case "customcommand2":
                    string playeroffset2 = $"{player.transform.position.x + 2},{player.transform.position.y},{player.transform.position.z + 1}";
                    string consolecommand2 = Settings.customCommands.CustomCommand2.Command
                        .Replace("{playername}", player.displayName)
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{playerlocation}", playeroffset2);


                    Server.Command(consolecommand2);

                    return Settings.customCommands.CustomCommand2.Message;

                case "customcommand3":
                    string playeroffset3 = $"{player.transform.position.x + 2},{player.transform.position.y},{player.transform.position.z + 1}";
                    string consolecommand3 = Settings.customCommands.CustomCommand3.Command
                        .Replace("{playername}", player.displayName)
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{playerlocation}", playeroffset3);


                    Server.Command(consolecommand3);

                    return Settings.customCommands.CustomCommand3.Message;

                case "customcommand4":
                    string playeroffset4 = $"{player.transform.position.x + 2},{player.transform.position.y},{player.transform.position.z + 1}";
                    string consolecommand4 = Settings.customCommands.CustomCommand4.Command
                        .Replace("{playername}", player.displayName)
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{playerlocation}", playeroffset4);


                    Server.Command(consolecommand4);

                    return Settings.customCommands.CustomCommand4.Message;

                case "customcommand5":
                    string playeroffset5 = $"{player.transform.position.x + 2},{player.transform.position.y},{player.transform.position.z + 1}";
                    string consolecommand5 = Settings.customCommands.CustomCommand5.Command
                        .Replace("{playername}", player.displayName)
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{playerlocation}", playeroffset5);


                    Server.Command(consolecommand5);

                    return Settings.customCommands.CustomCommand5.Message;

                case "customcommand6":
                    string playeroffset6 = $"{player.transform.position.x + 2},{player.transform.position.y},{player.transform.position.z + 1}";
                    string consolecommand6 = Settings.customCommands.CustomCommand6.Command
                        .Replace("{playername}", player.displayName)
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{playerlocation}", playeroffset6);


                    Server.Command(consolecommand6);

                    return Settings.customCommands.CustomCommand6.Message;

                case "customcommand7":
                    string playeroffset7 = $"{player.transform.position.x + 2},{player.transform.position.y},{player.transform.position.z + 1}";
                    string consolecommand7 = Settings.customCommands.CustomCommand7.Command
                        .Replace("{playername}", player.displayName)
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{playerlocation}", playeroffset7);


                    Server.Command(consolecommand7);

                    return Settings.customCommands.CustomCommand7.Message;

                case "customcommand8":
                    string playeroffset8 = $"{player.transform.position.x + 2},{player.transform.position.y},{player.transform.position.z + 1}";
                    string consolecommand8 = Settings.customCommands.CustomCommand8.Command
                        .Replace("{playername}", player.displayName)
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{playerlocation}", playeroffset8);


                    Server.Command(consolecommand8);

                    return Settings.customCommands.CustomCommand8.Message;

                case "customcommand9":
                    string playeroffset9 = $"{player.transform.position.x + 2},{player.transform.position.y},{player.transform.position.z + 1}";
                    string consolecommand9 = Settings.customCommands.CustomCommand9.Command
                        .Replace("{playername}", player.displayName)
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{playerlocation}", playeroffset9);


                    Server.Command(consolecommand9);

                    return Settings.customCommands.CustomCommand9.Message;

                case "customcommandA":
                    string playeroffsetA = $"{player.transform.position.x + 2},{player.transform.position.y},{player.transform.position.z + 1}";
                    string consolecommandA = Settings.customCommands.CustomCommandA.Command
                        .Replace("{playername}", player.displayName)
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{playerlocation}", playeroffsetA);


                    Server.Command(consolecommandA);

                    return Settings.customCommands.CustomCommandA.Message;

                // Payback Commands

                case "rocketman":
                    string rocketmancommand = "rm {playerid}"
                        .Replace("{playerid}", player.UserIDString);
                    Server.Command(rocketmancommand);
                    return GetLang("Payback:RocketManMessage", player);

                case "shark":
                    string sharkcommand = "jaws {playerid}"
                        .Replace("{playerid}", player.UserIDString);
                    Server.Command(sharkcommand);
                    return GetLang("Payback:SharkMessage", player);

                case "shocker":
                    player.SetHealth(10f);
                    string shockercommand = "sh {playerid}"
                        .Replace("{playerid}", player.UserIDString);
                    Server.Command(shockercommand);
                    timer.Once(5f, () =>
                    {
                        Server.Command(shockercommand);
                    });
                    return GetLang("Payback:ShockerMessage", player);

                case "naked":
                    string nakedcommand = "nk {playerid}"
                        .Replace("{playerid}", player.UserIDString);
                    Server.Command(nakedcommand);
                    return GetLang("Payback:NakedMessage", player);

                case "chickens":
                    string[] args = {
                        "",
                    };
                    Vector3 pos = GetPosFromButton(player, startloc, 3);
                    Payback.Call("SpawnChickens", player, pos, args);
                    return GetLang("Payback:ChickensMessage", player);

                //Payback2 commands
                case "train":

                    PlayerLookDown(player);
                    BlockMovement(player);
                    Payback2.Call("DoTrain", player, player, null);
                    return GetLang("Payback2:TrainMessage", player);

                case "f15":

                    string[] args2 = {
                        "",
                    };
                    Payback2.Call("DoAirstrike", player, player, args2);
                    return GetLang("Payback2:F15Message", player);

            }
            return pun;
        }

        void LoadPuns()
        {
            Punishments.Add("slap", Settings.punWeight.slapWeight);
            Punishments.Add("swim", Settings.punWeight.swimWeight);
            Punishments.Add("fall", Settings.punWeight.fallWeight);
            Punishments.Add("c4", Settings.punWeight.c4Weight);
            Punishments.Add("bear", Settings.punWeight.bearWeight);
            Punishments.Add("scientist", Settings.punWeight.scientistWeight);
            Punishments.Add("radiation", Settings.punWeight.radiationWeight);
            Punishments.Add("landmine", Settings.punWeight.landmineWeight);
            Punishments.Add("cactus", Settings.punWeight.cactusWeight);
            Punishments.Add("scrapheli", Settings.punWeight.scrapheliWeight);
            Punishments.Add("scarecrow", Settings.punWeight.scarecrowWeight);
            Punishments.Add("mlrs", Settings.punWeight.swimWeight);                      
            Punishments.Add("pvelord", Settings.punWeight.pvelordWeight);
            Punishments.Add("guntrap", Settings.punWeight.guntrapWeight);
            Punishments.Add("fireball", Settings.punWeight.fireballWeight);
            if (GetJunkYardMonument()) { Punishments.Add("shredder", Settings.punWeight.shredderWeight); }
            Punishments.Add("explosivediarrhea", Settings.punWeight.explosivediarrheaWeight);
            if (Payback != null && Settings.buttonSettings.usePayBack)
            {
                Punishments.Add("rocketman", Settings.punWeight.rocketmanWeight);
                Punishments.Add("shark", Settings.punWeight.sharkWeight);
                Punishments.Add("shocker", Settings.punWeight.shockerWeight);
                Punishments.Add("naked", Settings.punWeight.nakedWeight);
                Punishments.Add("chickens", Settings.punWeight.chickenWeight);
            }
            if (Payback2 != null && Settings.buttonSettings.usePayBack)
            {
                Punishments.Add("train", Settings.punWeight.trainWeight);
                Punishments.Add("f15", Settings.punWeight.f15Weight);

            }
            if (BombVest != null)
            {
                Punishments.Add("bombvest", Settings.punWeight.vestWeight);
            }
            foreach(var cc in GetCCList())
            {
                if(cc.Weight > 0)
                {
                    Punishments.Add(cc.CCname, cc.Weight);
                }
            }
        }

        public List<cc> GetCCList()
        {
            var cclist = new List<cc>();
            cclist.Add(Settings.customCommands.CustomCommand1);
            cclist.Add(Settings.customCommands.CustomCommand2);
            cclist.Add(Settings.customCommands.CustomCommand3);
            cclist.Add(Settings.customCommands.CustomCommand4);
            cclist.Add(Settings.customCommands.CustomCommand5);
            cclist.Add(Settings.customCommands.CustomCommand6);
            cclist.Add(Settings.customCommands.CustomCommand7);
            cclist.Add(Settings.customCommands.CustomCommand8);
            cclist.Add(Settings.customCommands.CustomCommand9);
            cclist.Add(Settings.customCommands.CustomCommandA);
            return cclist;
        }
        private void SendPlayerToPVE(BasePlayer player, Vector3 loc)
        {
            Teleport(player,loc);
        }

        private void PlayerLookDown(BasePlayer player)
        {
            var pos = new Vector3(0, -1, 0);
            pos.Normalize();
            player.ClientRPCPlayer<Vector3>(null, player, "ForceViewAnglesTo", pos);
        }

        private void BlockMovement(BasePlayer b)
        {
            CuiElementContainer elements = new CuiElementContainer();
            var panel = elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0"
                },

                RectTransform =
                {
                    AnchorMin = "0.3275 0.2677777",
                    AnchorMax = "0.625 0.9444445",
                },
                KeyboardEnabled = true,

            }, "Overlay", "StunMessage");

            CuiHelper.AddUi(b, elements);

            timer.Once(8, () =>
            {               
                if (b != null && b.IsConnected)
                {
                    CuiHelper.DestroyUi(b, "StunMessage");                   
                }
            });
        }


        public List<Vector3> GetCircumferencePositions(Vector3 center, float radius, float next, float y, bool PlayerPosition = false)
        {

            var positions = new List<Vector3>();
            float degree = 0f;
            int nDegreeRequired = 1;
            if (PlayerPosition)
            {
                int nTotalTraps = 6;
                nDegreeRequired = 360 / nTotalTraps;
            }

            while (degree < 360)
            {
                if (!PlayerPosition || (degree % nDegreeRequired) == 0)
                {
                    float angle = (float)(2 * Math.PI / 360) * degree;
                    float x = center.x + radius * (float)Math.Cos(angle);
                    float z = center.z + radius * (float)Math.Sin(angle);
                    var position = new Vector3(x, center.y, z);
                    if (PlayerPosition)
                        position.y = center.y;
                    else
                        position.y = y == 0f ? TerrainMeta.HeightMap.GetHeight(position) : y;
                    positions.Add(position);
                }
                degree += next;
            }

            return positions;
        }


        bool GetJunkYardMonument()
        {

            foreach (var monument in TerrainMeta.Path.Monuments)
            {
                if (monument == null) return false;

                if (monument.name == "assets/bundled/prefabs/autospawn/monument/medium/junkyard_1.prefab")
                {
                    ScrapYard = monument;
                    return true;
                }
              
            }

            return false;
        }
        bool GetPVEMonument()
        {
           
            foreach (var monument in TerrainMeta.Path.Monuments)
            {
                if (monument == null) return false;
               
                if (monument.name == "OilrigAI2")
                {
                    PVELordMonument = monument;
                    return true;
                }
                if (monument.name == "OilrigAI")
                {
                    PVELordMonument = monument;
                    return true;
                }
               
            }
           
            return false;
        }

        private static Vector3 GetPosFromButton(BasePlayer player, Vector3 startloc,int distance)
        {
            Vector3 dir = (player.transform.position - startloc).normalized;
            var finalpos = startloc + dir * distance;
            finalpos.y = TerrainMeta.HeightMap.GetHeight(finalpos);
            return finalpos;
        }

        void RemoveColliderProtection(BaseEntity colliderEntity)
        {
            foreach (var meshCollider in colliderEntity.GetComponentsInChildren<MeshCollider>())
            {
                UnityEngine.Object.DestroyImmediate(meshCollider);
            }

            UnityEngine.Object.DestroyImmediate(colliderEntity.GetComponent<GroundWatch>());
        }

        Vector3 SetupLabPos()
        {
            TerrainMeta.Path.Monuments.ForEach(monument =>
            {
                if (monument == null) return;
                if (monument.name.Contains("underwater_lab"))
                {
                    loc = new Vector3(monument.transform.position.x, 0, monument.transform.position.z);
                    return;
                }

            });

            return loc;
        }
        Vector3 FindSwimLocation(Vector3 loc)
        {
            for (int i = 0; i < 5000; i++)
            {
                if (loc.x > 0) { loc.x += 4f; } else { loc.x -= 4f; }
                if (loc.z > 0) { loc.z += 4f; } else { loc.z -= 4f; }
                if (TerrainMeta.HeightMap.GetHeight(loc)<0)
                {
                    if (loc.x > 0) { loc.x += 500f; } else { loc.x -= 500f; }
                    if (loc.z > 0) { loc.z += 500f; } else { loc.z -= 500f; }
                    break;
                }
            }
            return loc;
        }
        #endregion Core

        #region MLRS Logic


        private void ExecuteFireOperation(BasePlayer player, Vector3 targetPosition)
        {
            float baseGravity;
            Vector3 aimToTarget = GetAimToTarget(player.ServerPosition, targetPosition, out baseGravity);

            var startPoint = player.ServerPosition;
            startPoint.y += 15f;

            ServerProjectile projectile;

            if (CreateAndSpawnRocket(startPoint, aimToTarget, out projectile) == false)
                return;

            projectile.gravityModifier = baseGravity / (0f - Physics.gravity.y);
        }

        private Vector3 GetAimToTarget(Vector3 startPosition, Vector3 targetPos, out float baseGravity)
        {
            Vector3 vector = targetPos - startPosition;

            float num = 90f;
            float num2 = vector.Magnitude2D();
            float y = vector.y;
            float num5 = 40f;

            baseGravity = ProjectileDistToGravity(Mathf.Max(num2, 50f), y, num5, num);

            vector.Normalize();
            vector.y = 0f;

            Vector3 axis = Vector3.Cross(vector, Vector3.up);

            vector = Quaternion.AngleAxis(num5, axis) * vector;

            return vector;
        }

        private bool CreateAndSpawnRocket(Vector3 firingPos, Vector3 firingDir,
            out ServerProjectile mlrsRocketProjectile)
        {
            RaycastHit hitInfo;

            float launchOffset = 0f;

            if (Physics.Raycast(firingPos, firingDir, out hitInfo, launchOffset, 1236478737))
                launchOffset = hitInfo.distance - 0.1f;

            var mlrsRocketEntity = GameManager.server.CreateEntity(MLRSRocketPrefab, firingPos + firingDir * launchOffset);

            if (mlrsRocketEntity == null)
            {
                mlrsRocketProjectile = null;
                return false;
            }

            mlrsRocketProjectile = mlrsRocketEntity.GetComponent<ServerProjectile>();

            var velocityVector = mlrsRocketProjectile.initialVelocity + firingDir * mlrsRocketProjectile.speed;

            mlrsRocketProjectile.InitializeVelocity(velocityVector);

            var mlrsRocket = mlrsRocketEntity as MLRSRocket;

            if (mlrsRocket == null)
                return false;

            ExecuteMLRSRocketModfications(ref mlrsRocket);

            mlrsRocket.Spawn();

            return true;
        }

        private float ProjectileDistToGravity(float x, float y, float θ, float v)
        {
            float num = θ * ((float)Math.PI / 180f);
            float num2 = (v * v * x * Mathf.Sin(2f * num) - 2f * v * v * y * Mathf.Cos(num) * Mathf.Cos(num)) / (x * x);
            if (float.IsNaN(num2) || num2 < 0.01f)
            {
                num2 = 0f - Physics.gravity.y;
            }

            return num2;
        }
        private void ExecuteMLRSRocketModfications(ref MLRSRocket mlrsRocket)
        {
            mlrsRocket.explosionRadius *= 1;
            mlrsRocket.damageTypes = GetDamageOfRocket(mlrsRocket).damageTypes;
        }
        private TimedExplosive GetDamageOfRocket(MLRSRocket mlrsRocket)
        {
            if (mlrsRocketTimedExplosive != null)
                return mlrsRocketTimedExplosive;

            foreach (var damage in mlrsRocket.damageTypes)
                damage.amount *= 1;

            mlrsRocketTimedExplosive = new TimedExplosive
            {
                damageTypes = mlrsRocket.damageTypes
            };

            return mlrsRocketTimedExplosive;
        }
        #endregion MLRS Logic
            
        #region Diarrhea Logic

        void StartDiaria(BasePlayer target)
        {
           
            var entity = GameManager.server.CreateEntity(toiletPrefab, target.transform.position);
            var toilet = entity?.GetComponent<BaseMountable>();
            toilet.Spawn();
            target.mounted.Get(toilet);
            List<Item> poops = new List<Item>();
            var pos = new Vector3(target.transform.position.x, target.transform.position.y + 0.5f, target.transform.position.z);          
            StartScreaming(target);
            timer.Repeat(0.08f, 75, () =>
            {
                Item poop = ItemManager.CreateByItemID(-1579932985);
                if (poop == null) { Puts("poop is null"); return; }                              
                poop.DropAndTossUpwards(pos, 2);
                poops.Add(poop);
                poop.name = "0304";

            });

            timer.Once(6f, () =>
            {
                foreach (var item in poops)
                {
                    item.RemoveFromWorld();
                }
                Effect.server.Run(effect, target.transform.position);
                target.Die();
                entity.Kill();
            });
        }

        void StartScreaming(BasePlayer player)
        {

            PlayGesture(player);
            timer.Once(2f, () =>
            {
                if (player != null)
                    PlayGesture(player);
            });
            timer.Once(4f, () =>
            {
                if (player != null)
                    PlayGesture(player);
            });

            PlaySound(sound_scream, player, false);

        }

        public void PlaySound(string effect, BasePlayer player, bool playlocal = true, Vector3 posLocal = default(Vector3))
        {
            var sound = new Effect(effect, player, 0, Vector3.zero, Vector3.forward);

            if (posLocal != Vector3.zero)
            {
                sound = new Effect(effect, player.transform.position + posLocal, Vector3.forward);
            }


            if (playlocal)
            {
                EffectNetwork.Send(sound, player.net.connection);
            }
            else
            {
                EffectNetwork.Send(sound);
            }
        }

        public void PlayGesture(BasePlayer target)
        {
            if (target == null) return;
            GestureConfig toPlay = GestureCollection.Instance.IdToGesture(2554489267);
            target.Server_StartGesture(toPlay);
           
        }

        #endregion Diarrhea Logic

        #region FireBall

        void Fireball(BasePlayer player)
        {
            var fireBall = GameManager.server.CreateEntity(fireball, player.transform.position);
            var fireeffect = "assets/bundled/prefabs/fx/fire/fire_v3.prefab";           
            fireBall.Spawn();
            RemoveColliderProtection(fireBall);
            fireBall.SetFlag(BaseEntity.Flags.On, true);
            fireBall.limitNetworking = true;
            PlayerEffect playerEffect = player.gameObject.GetOrAddComponent<PlayerEffect>();
            playerEffect.effect = fireeffect;
            playerEffect.effectPosition = new Vector3(0, 0, 0);
            playerEffect.time = 12f;
            playerEffect.DestroyTimer();
            playerEffect.RunTimer();
            timer.Repeat(0.01f, 1000, () =>
            {
                if (fireBall == null) { return; }
                fireBall.transform.position = player.transform.position;
            });


            timer.Once(8f, () =>
            {
                DestroyPlayerComponent(player);
                if (player.IsAlive()) { player.Die(); }
                if (fireBall == null) { return; }
                fireBall.Kill();
            });
        }


        public class PlayerEffect : MonoBehaviour
        {
            public BasePlayer player;
            public string effect;
            public Vector3 effectPosition;
            public float time;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                effect = string.Empty;
                effectPosition = Vector3.zero;
                time = 0.2f;
            }

            public void RunTimer() => InvokeRepeating("RunEffect", 0.2f, time);

            public void DestroyTimer() => CancelInvoke("RunEffect");

            private void RunEffect()
            {
                if (string.IsNullOrEmpty(effect) || player == null)
                    return;

                Effect.server.Run(effect, player, 0, effectPosition, new Vector3(1, 0, 0), null, true);
            }

            private void OnDestroy()
            {
                DestroyTimer();
                Destroy(this);
            }
        }
        #endregion Fireball

        #region Discord Logic

        private void SendDiscordEmbed(/*byte[] image,*/ BasePlayer player, string punishment)
        {
            var title = FormatMessage("EmbedTitle", player,punishment );
            var description = FormatMessage("EmbedBody", player,punishment );

            var payload = new
            {
                embeds = new[]
                {
                    new
                    {
                        title,
                        //image = new
                        //{
                        //    url = "attachment://imageZwVkZQL=.png"
                        //},
                        description,
                        color = _embedColor,
                        timestamp = DateTime.Now
                    }
                }
            };

            var form = new WWWForm();
            
           // form.AddBinaryData("file", image, "image.png");
            form.AddField("payload_json", JsonConvert.SerializeObject(payload));

            ServerMgr.Instance.StartCoroutine(HandleUpload(Settings.buttonSettings.discordWebHook, form));
        }

        private IEnumerator HandleUpload(string url, WWWForm data)
        {
            var www = UnityWebRequest.Post(url, data);
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Puts($"Failed to post image to discord: {www.error}");
            }
        }

        private string FormatMessage(string key, BasePlayer player, string punishment)
        {
            return lang.GetMessage(key, this)
                .Replace("{playerName}", player.displayName)
                .Replace("{playerId}", player.UserIDString)
                .Replace("{punishment}", punishment)
                .Replace("{serverName}", covalence.Server.Name)
                .Replace("{punName}",PunName);
                
        }

        private static int? FromHex(string value)
        {
            var match = Regex.Match(value, "#?([0-9a-f]{6})");
            if (!match.Success)
            {
                return null;
            }

            return int.Parse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber);
        }


        #endregion Discord Logic

        #region Commands


        [ChatCommand("button")]
        private void buttonCommand(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, permUse))
            {
                var button = ItemManager.CreateByName("sign.post.double", 1);
                button.name = "The Red Button";
                button.skin = skinIDButton;
                
              
                player.GiveItem(button);
            }

        }
        [ChatCommand("spawnbutton")]
        private void sbuttonCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) { return; }    

            var layers = LayerMask.GetMask("Construction", "Default", "Deployed", "Resource", "Terrain", "Water", "World","Clutter");


            RaycastHit hit = new RaycastHit();

            if (UnityEngine.Physics.Raycast(player.eyes.HeadRay(), out hit, float.MaxValue, layers))
            {
                var hitpoint = hit.point;

                if (hitpoint != null)
                {
                    Vector3 relativePos = player.transform.position - hitpoint;
                    Quaternion rotation = Quaternion.LookRotation(relativePos, Vector3.up);

                   
                    SpawnButton(player.userID, hitpoint, rotation);

                }
            }
        }
      

        [ChatCommand("clearallbuttons")]
        private void clearbuttonCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) { return; }
            var list = cachedButtons.Keys.ToList();
            foreach (var bt in list)
            {
                NetworkableId buttonid = new NetworkableId(ulong.Parse(bt.ToString()));
                if (buttonid ==null) { continue; }
               
                Signage entity = Signage.serverEntities.Find(buttonid) as Signage;
                if(entity == null) { continue; }

                if (entity != null)
                {
                    var buttons1 = Pool.Get<List<PressButton>>();
                    // Vis.Entities(entity.transform.position, 0.5f, buttons1);
                    foreach (var bb in PressButton.serverEntities)
                    {
                        if (Vector3.Distance(entity.transform.position, bb.transform.position) < 0.5f)
                        {
                            buttons1.Add(bb as PressButton);
                        }
                    }
                    foreach (var b in buttons1)
                    {
                        if (b == null) continue;
                        if (b.OwnerID == entity.OwnerID)
                        {
                            b.Kill();
                        }
                    }
                    Pool.FreeUnmanaged(ref buttons1);
                    entity.Kill();
                    cachedButtons.Clear();
                    player.ChatMessage("Allbuttons cleared and removed");
                }
            }
        }

        #endregion Commands

        #region UI

        #region main ui
        private CuiElementContainer genarate_ButtonMenu(BasePlayer player)
        {
            var elements = new CuiElementContainer();

            var panel = elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.7"
                },

                RectTransform =
                {
                    AnchorMin = "0.24 0.24",
                    AnchorMax = "0.75 0.90",
                },

                CursorEnabled = true
            }, "Overlay", "buttonmenu_panel");

            var Image = new BackgroundImageSettings();
            var backgroundImage = CreateImage(panel, Image);

            elements.Add(backgroundImage);
            var topbar = elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.9"
                },

                RectTransform =
                {
                    AnchorMin = "0 0.95",
                    AnchorMax = "1 1",
                },
            }, panel);

            var activepanel = elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0"
                },

                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 0.95",
                },
            }, panel);          

            var Button_close = elements.Add(new CuiButton
            {
                Button =
                {
                   Command = $"button.exit" ,
                   Color = "0.8 0 0 0.8"
                },


                RectTransform =
                {
                    AnchorMin = "0.95 0.12",
                    AnchorMax = "0.99 0.85"
                },

                Text =
                {
                    Text = $"X",
                    FontSize = 11,
                    Align = TextAnchor.MiddleCenter
                }
            }, topbar);

            var Top_text = elements.Add(new CuiLabel
            {
                
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "0.2 1"
                    
                },

                Text =
                {
                    Text = $"The Red Button",
                    Color = "1 0 0 1",
                    FontSize = 16,
                    Align = TextAnchor.MiddleCenter
                }
            }, topbar);


            #region player online Buttons


            double ancmin = 0.007163225;
            double anczmin = 0.9630515;
            double ancmax = 0.12224492;
            double anczmax = 0.991903325;

            foreach (var p in BasePlayer.activePlayerList.OrderBy(x => x.displayName))
            {

                var Button_name = elements.Add(new CuiButton
                {
                    Button =
                    {
                       Command = $"button.selected {p.UserIDString}" ,
                       Color = "0 0 0 0.8"
                    },


                    RectTransform =
                    {
                        AnchorMin = $"{ancmin} {anczmin}",
                        AnchorMax = $"{ancmax} {anczmax}"
                    },

                    Text =
                    {
                        Text = $"{p.displayName}",
                        FontSize = 11,
                        Align = TextAnchor.MiddleCenter
                    }
                }, activepanel);


                if (ancmax >= 0.90)
                {
                    anczmin = anczmin - 0.035;
                    anczmax = anczmax - 0.035;
                    ancmin = 0.007163225 - 0.14;
                    ancmax = 0.12224492 - 0.14;
                }
                ancmin = ancmin + 0.14;
                ancmax = ancmax + 0.14;
            }

            #endregion players online Buttons


            return elements;
        }
        #endregion  mainui

        #region selected ui
        private CuiElementContainer genarate_SelectedMenu(BasePlayer player,BasePlayer target)
        {
            var elements = new CuiElementContainer();

            var panel = elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.7"
                },

                RectTransform =
                {
                    AnchorMin = "0.24 0.24",
                    AnchorMax = "0.75 0.90",
                },

                CursorEnabled = true
            }, "Overlay", "selectedmenu_panel");

            var Image = new BackgroundImageSettings();
            var backgroundImage = CreateImage(panel, Image);
            elements.Add(backgroundImage);

            var topbar = elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.9"
                },

                RectTransform =
                {
                    AnchorMin = "0 0.95",
                    AnchorMax = "1 1",
                },
            }, panel);
            var activepanel = elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0"
                },

                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 0.95",
                },
            }, panel);

            

            var Button_close = elements.Add(new CuiButton
            {
                Button =
                {
                   Command = $"button.exit" ,
                   Color = "0.8 0 0 0.8"
                },


                RectTransform =
                {
                    AnchorMin = "0.95 0.12",
                    AnchorMax = "0.99 0.85"
                },

                Text =
                {
                    Text = $"X",
                    FontSize = 11,
                    Align = TextAnchor.MiddleCenter
                }
            }, topbar);

            var Top_text = elements.Add(new CuiLabel
            {

                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "0.2 1"

                },

                Text =
                {
                    Text = $"The Red Button",
                    Color = "1 0 0 1",
                    FontSize = 16,
                    Align = TextAnchor.MiddleCenter
                }
            }, topbar);


            #region Punishment Buttons


            double ancmin = 0.007163225;
            double anczmin = 0.9630515;
            double ancmax = 0.14224492;
            double anczmax = 0.991903325;

            foreach (var p in Punishments.OrderBy(x =>x.Key))
            {

                var Button_name = elements.Add(new CuiButton
                {
                    Button =
                    {
                       Command = $"button.punish {target.UserIDString} {p.Key}" ,
                       Color = "0 0 0 0.8"
                    },


                    RectTransform =
                    {
                        AnchorMin = $"{ancmin} {anczmin}",
                        AnchorMax = $"{ancmax} {anczmax}"
                    },

                    Text =
                    {
                        Text = $"{p.Key}",
                        FontSize = 11,
                        Align = TextAnchor.MiddleCenter
                    }
                }, activepanel);


                if (ancmax >= 0.80)
                {
                    anczmin = anczmin - 0.035;
                    anczmax = anczmax - 0.035;
                    ancmin = 0.007163225 - 0.145;
                    ancmax = 0.14224492 - 0.145;
                }
                ancmin = ancmin + 0.145;
                ancmax = ancmax + 0.145;
            }

            #endregion Punishment Buttons


            return elements;
        }
        #endregion  selected ui


        #region menu commands



        [ChatCommand("buttonui")]
        private void buttonuiopenmenuCommand(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, permAdmin)||player.IsAdmin)
            {
                CuiElementContainer buttonmenu = genarate_ButtonMenu(player);
                CuiHelper.AddUi(player, buttonmenu);
            }
        }

        [ConsoleCommand("button.exit")]
        private void eventstopmenuCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            CuiHelper.DestroyUi(player, "buttonmenu_panel");
            CuiHelper.DestroyUi(player, "selectedmenu_panel");
        }

        [ConsoleCommand("button.selected")]
        private void selectedplayerCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var command = arg.GetString(0);
            
            var target = findPlayer(command);
            CuiHelper.DestroyUi(player, "buttonmenu_panel");
            CuiElementContainer buttonmenu = genarate_SelectedMenu(player,target);
            CuiHelper.AddUi(player, buttonmenu);
        }

        [ConsoleCommand("button.punish")]
        private void punishplayerCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var command = arg.GetString(0);
            var key = arg.GetString(1);
            var target = findPlayer(command);
            CuiHelper.DestroyUi(player, "selectedmenu_panel");
            DoPunishment(target, player.transform.position, key);
        }


        private static CuiElement CreateImage(string panelName, ImageSettings settings)
        {
            var element = new CuiElement();
            var image = new CuiRawImageComponent
            {
                Url = settings.Url,
                Color = string.Format("1 1 1 {0:F1}", (settings.TransparencyInPercent / 100.0f))
            };

            var position = settings.Position;
            var rectTransform = new CuiRectTransformComponent
            {
                AnchorMin = position.GetRectTransformAnchorMin(),
                AnchorMax = position.GetRectTransformAnchorMax()
            };
            element.Components.Add(image);
            element.Components.Add(rectTransform);
            element.Name = CuiHelper.GetGuid();
            element.Parent = panelName;

            return element;
        }

        public class ImageSettings
        {
            public Position Position { get; set; }
            public string Url { get; set; }
            public int TransparencyInPercent { get; set; }

            public ImageSettings()
            {
                Position = new Position
                {
                    MaxX = 1.0f,
                    MaxY = 1.0f,
                    MinY = 0.0f,
                    MinX = 0.0f
                };
                Url = "http://www.noobhub.co.za/signbg.png";
                TransparencyInPercent = 100;
            }
        }
        public sealed class BackgroundImageSettings : ImageSettings
        {
            public bool Enabled { get; set; }

            public BackgroundImageSettings()
            {
                Enabled = false;
            }
        }
        public sealed class Position
        {
            public Position()
            {
                MinX = 0.15f;
                MaxX = 0.9f;
                MinY = 0.2f;
                MaxY = 0.9f;
            }

            public float MinX { get; set; }
            public float MaxX { get; set; }
            public float MinY { get; set; }
            public float MaxY { get; set; }

            public string GetRectTransformAnchorMin()
            {
                return string.Format("{0} {1}", MinX, MinY);
            }

            public string GetRectTransformAnchorMax()
            {
                return string.Format("{0} {1}", MaxX, MaxY);
            }
        }


        #endregion menu commands

        #endregion UI      

        #region Config
        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            Config.WriteObject(DefaultConfig(), true);

            PrintWarning("Default Configuration File Created");
        }
        void SaveUpdatedConfig()
        {
            Config.Clear();
            Config.WriteObject(Settings, true);
            PrintWarning("Updated Configuration File Loaded");
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            Settings = Config.ReadObject<PluginConfig>();
            Config.WriteObject(Settings);
        }
        private void LoadConfigValues()
        {
            Settings = Config.ReadObject<PluginConfig>();
        }

        private class PluginConfig
        {
            [JsonProperty(PropertyName = "1. The Button Settings")]
            public ButtonSettings buttonSettings { get; set; }

            [JsonProperty(PropertyName = "2. Punishment Weights")]
            public PunWeight punWeight { get; set; }

            [JsonProperty(PropertyName = "3. Custom Commands")]
            public CustomCommands customCommands { get; set; }

            public void UpdatePunWeight(string propertyName, int newValue)
            {
                var property = punWeight.GetType().GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (property != null)
                {
                    property.SetValue(punWeight, newValue);                   
                }
                else
                {
                   
                }
            }
        }
        
        public class ButtonSettings
        {
            [JsonProperty(PropertyName = "1. Sign Picture URL")]
            public string PictureURL { get; set; }

            [JsonProperty(PropertyName = "2. Cooldown Time on Button Press")]
            public int buttonCooldown { get; set; }

            [JsonProperty(PropertyName = "3. Broadcast Button Action to Server")]
            public bool broadcastButtonAction { get; set; }

            [JsonProperty(PropertyName = "4. Broadcast Font Size [18 default]")]
            public int broadcastFontSize { get; set; }

            [JsonProperty(PropertyName = "5. Send Discord embeds on punishments")]
            public bool sendDiscord { get; set; }

            [JsonProperty(PropertyName = "6. Discord WebHook URL")]
            public string discordWebHook { get; set; }

            [JsonProperty(PropertyName = "7. Use Payback Plugin Punishments")]
            public bool usePayBack { get; set; }

            [JsonProperty(PropertyName = "8. Timed C4 (false will be instant explosion")]
            public bool c4Timed { get; set; }

            [JsonProperty(PropertyName = "9. Use Random location for swim location")]
            public bool locSwimRandom { get; set; }

        }

        public class PunWeight
        {
            [JsonProperty(PropertyName = "Slap")]
            public int slapWeight { get; set; }

            [JsonProperty(PropertyName = "Mlrs")]
            public int mlrsWeight { get; set; }           

            [JsonProperty(PropertyName = "Swim")]
            public int swimWeight { get; set; }

            [JsonProperty(PropertyName = "Fall")]
            public int fallWeight { get; set; }

            [JsonProperty(PropertyName = "C4")]
            public int c4Weight { get; set; }

            [JsonProperty(PropertyName = "Bear")]
            public int bearWeight { get; set; }

            [JsonProperty(PropertyName = "Scientist")]
            public int scientistWeight { get; set; }

            [JsonProperty(PropertyName = "Scarecrow")]
            public int scarecrowWeight { get; set; }

            [JsonProperty(PropertyName = "Radiation")]
            public int radiationWeight { get; set; }

            [JsonProperty(PropertyName = "Landmine")]
            public int landmineWeight { get; set; }

            [JsonProperty(PropertyName = "Cactus")]
            public int cactusWeight { get; set; }

            [JsonProperty(PropertyName = "Scrapheli")]
            public int scrapheliWeight { get; set; }

            [JsonProperty(PropertyName = "PVELord")]
            public int pvelordWeight { get; set; }

            [JsonProperty(PropertyName = "Shredder")]
            public int shredderWeight { get; set; }

            [JsonProperty(PropertyName = "Guntrap")]
            public int guntrapWeight { get; set; }

            [JsonProperty(PropertyName = "Fireball")]
            public int fireballWeight { get; set; }

            [JsonProperty(PropertyName = "ExplosiveDiarrhea")]
            public int explosivediarrheaWeight { get; set; }

            [JsonProperty(PropertyName = "PayBack:Rocketman")]
            public int rocketmanWeight { get; set; }

            [JsonProperty(PropertyName = "Payback:Shark")]
            public int sharkWeight { get; set; }

            [JsonProperty(PropertyName = "Payback:Shocker")]
            public int shockerWeight { get; set; }

            [JsonProperty(PropertyName = "Payback:Naked")]
            public int nakedWeight{ get; set; }

            [JsonProperty(PropertyName = "Payback:Chicken")]
            public int chickenWeight { get; set; }

            [JsonProperty(PropertyName = "Payback2:Train")]
            public int trainWeight { get; set; }

            [JsonProperty(PropertyName = "Payback2:F15")]
            public int f15Weight { get; set; }

            [JsonProperty(PropertyName = "BombVest:Boom")]
            public int vestWeight { get; set; }

            public KeyValuePair<string, int>[] ToArray()
            {
                var properties = GetType().GetProperties();
                var array = new KeyValuePair<string, int>[properties.Length];

                for (int i = 0; i < properties.Length; i++)
                {
                    var property = properties[i];
                    var key = (property.GetCustomAttributes(typeof(JsonPropertyAttribute), true).FirstOrDefault() as JsonPropertyAttribute)?.PropertyName ?? property.Name;
                    var value = (int)property.GetValue(this);

                    array[i] = new KeyValuePair<string, int>(key, value);
                }

                return array;
            }
            public string GetVariableName(string jsonPropertyName)
            {
                var property = GetType().GetProperties().FirstOrDefault(p => (p.GetCustomAttributes(typeof(JsonPropertyAttribute), true).FirstOrDefault() as JsonPropertyAttribute)?.PropertyName == jsonPropertyName);
                return property?.Name;
            }
        }
       
        public class CustomCommands
        {
            public cc CustomCommand1 { get; set; }
            public cc CustomCommand2 { get; set; }
            public cc CustomCommand3 { get; set; }
            public cc CustomCommand4 { get; set; }
            public cc CustomCommand5 { get; set; }
            public cc CustomCommand6 { get; set; }
            public cc CustomCommand7 { get; set; }
            public cc CustomCommand8 { get; set; }
            public cc CustomCommand9 { get; set; }
            public cc CustomCommandA { get; set; }

        }

        public class cc
        {
            [JsonProperty(PropertyName = "CCname (DONT CHANGE THIS)")]
            public string CCname { get; set; }
            public string Command { get; set; }
            public string Message { get; set; }
            public int Weight { get; set; }
        }

        private PluginConfig DefaultConfig()
        {
            return new PluginConfig
            {               
                buttonSettings = new ButtonSettings
                {
                    PictureURL = "www.noobhub.co.za/dont.png",
                    buttonCooldown = 5,
                    broadcastButtonAction = true,
                    broadcastFontSize = 18,
                    sendDiscord = false,
                    discordWebHook = "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
                    usePayBack = false,
                    locSwimRandom = true,
                    c4Timed = true,
                },
                punWeight = new PunWeight
                {
                    slapWeight = 2,
                    mlrsWeight = 2,                    
                    fallWeight = 2,
                    scarecrowWeight = 2,
                    scientistWeight = 2,
                    scrapheliWeight = 2,
                    sharkWeight = 2,
                    shockerWeight = 2,
                    shredderWeight = 2,
                    swimWeight = 2,
                    bearWeight = 2, 
                    c4Weight = 2,
                    cactusWeight = 2,
                    explosivediarrheaWeight = 2,    
                    fireballWeight = 2, 
                    guntrapWeight = 2,
                    landmineWeight = 2,
                    nakedWeight = 2,
                    pvelordWeight = 2,
                    radiationWeight = 2,
                    rocketmanWeight = 2,
                    chickenWeight = 2,
                    trainWeight = 2,
                    f15Weight = 2,
                    vestWeight = 2,
                },
                customCommands = new CustomCommands
                {
                    CustomCommand1 = new cc
                    {
                        CCname = "customcommand1",
                        Command = "giverank {playerid}",
                        Message = "Just got a Rank",
                        Weight = 0,
                    },
                    CustomCommand2 = new cc
                    {
                        CCname = "customcommand2",
                        Command = "",
                        Message = "",
                        Weight = 0,
                    },
                    CustomCommand3 = new cc
                    {
                        CCname = "customcommand3",
                        Command = "",
                        Message = "",
                        Weight = 0,
                    },
                    CustomCommand4 = new cc
                    {
                        CCname = "customcommand4",
                        Command = "",
                        Message = "",
                        Weight = 0,
                    },
                    CustomCommand5 = new cc
                    {
                        CCname = "customcommand5",
                        Command = "",
                        Message = "",
                        Weight = 0,
                    },
                    CustomCommand6 = new cc
                    {
                        CCname = "customcommand6",
                        Command = "",
                        Message = "",
                        Weight = 0,
                    },
                    CustomCommand7 = new cc
                    {
                        CCname = "customcommand7",
                        Command = "",
                        Message = "",
                        Weight = 0,
                    },
                    CustomCommand8 = new cc
                    {
                        CCname = "customcommand8",
                        Command = "",
                        Message = "",
                        Weight = 0,
                    },
                    CustomCommand9 = new cc
                    {
                        CCname = "customcommand9",
                        Command = "",
                        Message = "",
                        Weight = 0,
                    },
                    CustomCommandA = new cc
                    {
                        CCname = "customcommandA",
                        Command = "",
                        Message = "",
                        Weight = 0,
                    },
                },                
            };
        }

        #region Config UI
       
        private void CreateSettingsUI(BasePlayer player)
        {
            var container = UI.CreateElementContainer("MainPanel", "0.1 0.1 0.1 0.98", "0.01 0.01", "0.99 0.99");
            UI.Button(ref container, "MainPanel", "X", 11, "0.9075628 0.9441707", "0.9705881 0.9802954", "close.settings", TextAnchor.MiddleCenter, "0.8 0 0 0.8");
            UI.Label(ref container, "MainPanel", "Main Settings", 24, "0.00420168 0.9359605", "0.8865546 0.9950739", "1 0 0 1", TextAnchor.MiddleCenter);
            UI.Panel(ref container, "activeWeightpanel", "MainPanel", "0.3 0.3 0.3 0.7", "0.006 0.16", "0.30 0.84");           
            UI.Panel(ref container, "activeCustompanel", "MainPanel", "0.3 0.3 0.3 0.7", "0.31 0.16", "0.99 0.84");

            var weights = Settings.punWeight.ToArray();
            
            
            int numRows = 14; // Number of rows
            int WnumColumns = 2; // Number of columns weights
            int CnumColumns = 1; // Number of columns commands


            float startY = 0.87f; 
            float rowHeight = 0.06f; 

            float startX1 = 0.01f; 
            float startX2 = 0.51f; 
            float WcolumnWidth = 0.48f; 
            float CcolumnWidth = 0.98f; 


            for (int row = 0; row < numRows; row++)
            {
                for (int col = 0; col < WnumColumns; col++)
                {
                    int entryIndex = row * WnumColumns + col;

                    if (entryIndex >= weights.Length)
                        break; 
                   
                    float entryY = startY - row * rowHeight;
                    float entryX = col == 0 ? startX1 : startX2;
                    var entry = weights[entryIndex];
                   
                    UI.Label(ref container, "activeWeightpanel", $"{entry.Key}", 12, $"{entryX} {entryY - 0.01}", $"{entryX + WcolumnWidth / 2} {entryY + rowHeight}", "1 1 1 1");

                    UI.Panel(ref container, $"bgPanel{entryIndex}", "activeWeightpanel", "0.9 0.9 0.9 0.3", $"{entryX + WcolumnWidth / 2} {entryY - 0.01}", $"{entryX + WcolumnWidth} {entryY + rowHeight}");

                    UI.InputField(ref container, "activeWeightpanel", $"{entry.Value}", 12, $"{entryX + WcolumnWidth / 2} {entryY - 0.01}", $"{entryX + WcolumnWidth} {entryY + rowHeight}", $"weight.input {entry.Key}", TextAnchor.MiddleRight, "1 1 0 1");

                }
            }
            
            List<CustomCommand> commands = GetCustomCommands();

            for (int row = 0; row < numRows; row++)
            {
                for (int col = 0; col < CnumColumns; col++)
                {
                    int commandIndex = row * CnumColumns + col;

                    if (commandIndex >= commands.Count)
                        break; 

                    float entryY = startY - row * rowHeight;
                    float entryX = col == 0 ? startX1 : startX2;

                    var command = commands[commandIndex];

                    UI.Label(ref container, "activeCustompanel", $"{command.Number}", 12, $"{entryX} {entryY - 0.01}", $"{entryX + CcolumnWidth * 0.1f} {entryY + rowHeight}","1 1 0 1");

                    UI.Panel(ref container, $"bgPanelname{commandIndex}", "activeCustompanel", "0.9 0.9 0.9 0.3", $"{entryX + CcolumnWidth * 0.1f} {entryY - 0.01}", $"{entryX + CcolumnWidth * 0.5f} {entryY + rowHeight}");

                    UI.InputField(ref container, "activeCustompanel", $"{command.Name}", 12, $"{entryX + CcolumnWidth * 0.1f} {entryY - 0.01}", $"{entryX + CcolumnWidth * 0.5f} {entryY + rowHeight}", $"customcommand.input {command.Number}", TextAnchor.MiddleCenter, "1 1 0 1");

                    UI.Panel(ref container, $"bgPanelname{commandIndex}", "activeCustompanel", "0.9 0.9 0.9 0.3", $"{entryX + CcolumnWidth * 0.5f} {entryY - 0.01}", $"{entryX + CcolumnWidth * 0.9f} {entryY + rowHeight}");

                    UI.InputField(ref container, "activeCustompanel", $"{command.Message}", 12, $"{entryX + CcolumnWidth * 0.5f} {entryY - 0.01}", $"{entryX + CcolumnWidth * 0.9f} {entryY + rowHeight}", $"custommessage.input {command.Number}", TextAnchor.MiddleCenter, "1 1 0 1");

                    UI.Panel(ref container, $"bgPanelname{commandIndex}", "activeCustompanel", "0.9 0.9 0.9 0.3", $"{entryX + CcolumnWidth * 0.9f} {entryY - 0.01}", $"{entryX + CcolumnWidth} {entryY + rowHeight}");

                    UI.InputField(ref container, "activeCustompanel", $"{command.Weight}", 12, $"{entryX + CcolumnWidth * 0.9f} {entryY - 0.01}", $"{entryX + CcolumnWidth} {entryY + rowHeight}", $"customweight.input {command.Number}", TextAnchor.MiddleRight, "1 1 0 1");
                }
            }


            CuiHelper.AddUi(player, container);
        }
        public class CustomCommand
        {
            public int Number { get; set; }
            public string Name { get; set; }
            public string Message { get; set; }
            public int Weight { get; set; }
        }
       
        public List<CustomCommand> GetCustomCommands()
        {          
            return new List<CustomCommand>
            {
                new CustomCommand { Number = 1, Name = Settings.customCommands.CustomCommand1.Command,Message = Settings.customCommands.CustomCommand1.Message, Weight = Settings.customCommands.CustomCommand1.Weight },
                new CustomCommand { Number = 2, Name = Settings.customCommands.CustomCommand2.Command, Message = Settings.customCommands.CustomCommand2.Message,Weight = Settings.customCommands.CustomCommand2.Weight },
                new CustomCommand { Number = 3, Name = Settings.customCommands.CustomCommand3.Command,Message = Settings.customCommands.CustomCommand3.Message, Weight = Settings.customCommands.CustomCommand3.Weight },
                new CustomCommand { Number = 4, Name = Settings.customCommands.CustomCommand4.Command,Message = Settings.customCommands.CustomCommand4.Message, Weight = Settings.customCommands.CustomCommand4.Weight },
                new CustomCommand { Number = 5, Name = Settings.customCommands.CustomCommand5.Command, Message = Settings.customCommands.CustomCommand5.Message,Weight = Settings.customCommands.CustomCommand5.Weight },
                new CustomCommand { Number = 6, Name = Settings.customCommands.CustomCommand6.Command, Message = Settings.customCommands.CustomCommand6.Message,Weight = Settings.customCommands.CustomCommand6.Weight },
                new CustomCommand { Number = 7, Name = Settings.customCommands.CustomCommand7.Command, Message = Settings.customCommands.CustomCommand7.Message,Weight = Settings.customCommands.CustomCommand7.Weight },
                new CustomCommand { Number = 8, Name = Settings.customCommands.CustomCommand8.Command, Message = Settings.customCommands.CustomCommand8.Message,Weight = Settings.customCommands.CustomCommand8.Weight },
                new CustomCommand { Number = 9, Name = Settings.customCommands.CustomCommand9.Command, Message = Settings.customCommands.CustomCommand9.Message,Weight = Settings.customCommands.CustomCommand9.Weight},
                new CustomCommand { Number = 10, Name = Settings.customCommands.CustomCommandA.Command, Message = Settings.customCommands.CustomCommandA.Message,Weight = Settings.customCommands.CustomCommandA.Weight },
            };
        }

        private void UpdateUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "MainPanel");
            CreateSettingsUI(player);
        }
        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "MainPanel");
        }
        private void DestroySettingsUI()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "MainPanel");
            }
        }

        [ConsoleCommand("close.settings")]
        private void stopmenuCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            CuiHelper.DestroyUi(player, "MainPanel");
            SaveUpdatedConfig();
        }

        [ConsoleCommand("weight.input")]
        private void weightinputCommand(ConsoleSystem.Arg arg)
        {
            var entry = arg.GetString(0);
            var value = arg.GetString(1);
            int m;
            if (Int32.TryParse(value, out m))
            {
                var variableName = Settings.punWeight.GetVariableName(entry);
                Settings.UpdatePunWeight(variableName, m);                
            }
          
        }
        [ConsoleCommand("customweight.input")]
        private void commandinputCommand(ConsoleSystem.Arg arg)
        {
            var entry = arg.GetString(0);
            var value = arg.GetString(1);
            int m;
           
            switch (entry)
            {
                case "1":
                    if (Int32.TryParse(value, out m))
                    {
                        Settings.customCommands.CustomCommand1.Weight = m;
                    }

                    break;
                case "2":
                    if (Int32.TryParse(value, out m))
                    {
                        Settings.customCommands.CustomCommand2.Weight = m;
                    }
                    break;
                case "3":
                    if (Int32.TryParse(value, out m))
                    {
                        Settings.customCommands.CustomCommand3.Weight = m;
                    }
                    break;
                case "4":
                    if (Int32.TryParse(value, out m))
                    {
                        Settings.customCommands.CustomCommand4.Weight = m;
                    }
                    break;
                case "5":
                    if (Int32.TryParse(value, out m))
                    {
                        Settings.customCommands.CustomCommand5.Weight = m;
                    }
                    break;
                case "6":
                    if (Int32.TryParse(value, out m))
                    {
                        Settings.customCommands.CustomCommand6.Weight = m;
                    }
                    break;
                case "7":
                    if (Int32.TryParse(value, out m))
                    {
                        Settings.customCommands.CustomCommand7.Weight = m;
                    }
                    break;
                case "8":
                    if (Int32.TryParse(value, out m))
                    {
                        Settings.customCommands.CustomCommand8.Weight = m;
                    }
                    break;
                case "9":
                    if (Int32.TryParse(value, out m))
                    {
                        Settings.customCommands.CustomCommand9.Weight = m;
                    }
                    break;
                case "10":
                    if (Int32.TryParse(value, out m))
                    {
                        Settings.customCommands.CustomCommandA.Weight = m;
                    }

                    break;
            }
        }
        [ConsoleCommand("customcommand.input")]
        private void customweightinputCommand(ConsoleSystem.Arg arg)
        {
            var entry = arg.GetString(0);
            var value = arg.GetString(1);
            switch (entry)
            {
                case "1":
                   Settings.customCommands.CustomCommand1.Command = value;

                    break;
                case "2":
                    Settings.customCommands.CustomCommand2.Command = value;

                    break;
                case "3":
                    Settings.customCommands.CustomCommand3.Command = value;

                    break;
                case "4":
                    Settings.customCommands.CustomCommand4.Command = value;

                    break;
                case "5":
                    Settings.customCommands.CustomCommand5.Command = value;

                    break;
                case "6":
                    Settings.customCommands.CustomCommand6.Command = value;

                    break;
                case "7":
                    Settings.customCommands.CustomCommand7.Command = value;

                    break;
                case "8":
                    Settings.customCommands.CustomCommand8.Command = value;

                    break;
                case "9":
                    Settings.customCommands.CustomCommand9.Command = value;

                    break;
                case "10":
                    Settings.customCommands.CustomCommandA.Command = value;

                    break;
            }
        }
        [ConsoleCommand("custommessage.input")]
        private void custommessageinputCommand(ConsoleSystem.Arg arg)
        {
            var entry = arg.GetString(0);
            var value = arg.GetString(1);
            switch (entry)
            {
                case "1":
                    Settings.customCommands.CustomCommand1.Message = value;

                    break;
                case "2":
                    Settings.customCommands.CustomCommand2.Message = value;

                    break;
                case "3":
                    Settings.customCommands.CustomCommand3.Message = value;

                    break;
                case "4":
                    Settings.customCommands.CustomCommand4.Message = value;

                    break;
                case "5":
                    Settings.customCommands.CustomCommand5.Message = value;

                    break;
                case "6":
                    Settings.customCommands.CustomCommand6.Message = value;

                    break;
                case "7":
                    Settings.customCommands.CustomCommand7.Message = value;

                    break;
                case "8":
                    Settings.customCommands.CustomCommand8.Message = value;

                    break;
                case "9":
                    Settings.customCommands.CustomCommand9.Message = value;

                    break;
                case "10":
                    Settings.customCommands.CustomCommandA.Message = value;

                    break;
            }
        }

        [ChatCommand("buttonconfig")]
        private void testconfigCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString,permAdmin)) { return; }
            CreateSettingsUI(player);
        }
        static class UI
        {
            public static CuiElementContainer CreateElementContainer(string name, string color, string aMin, string aMax)
            {
                var element = new CuiElementContainer()
                    {
                        {
                            new CuiPanel
                            {
                                Image = { Color = color },
                                RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                                CursorEnabled = true,
                                KeyboardEnabled = true,
                            },
                            new CuiElement().Parent = "Overlay",
                            name
                        }
                    };
                return element;
            }
            public static void Panel(ref CuiElementContainer container, string name, string panel, string color, string aMin, string aMax)
            {
                container.Add(new CuiPanel
                {

                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                },
                panel, name);
            }
            public static void Label(ref CuiElementContainer container, string panel, string text, int size, string aMin, string aMax, string color, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Color = color, Font = "robotocondensed-regular.ttf", Align = align, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel, CuiHelper.GetGuid());
            }
            public static void Button(ref CuiElementContainer container, string panel, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter, string color = null)
            {
                container.Add(new CuiButton
                {
                    Button =
                {
                   Command = command,
                   Color = color,
                },


                    RectTransform =
                {
                    AnchorMin = aMin,
                    AnchorMax = aMax,
                },

                    Text =
                {
                    Text = text,
                    FontSize = size,
                    Align = align
                }
                },
                panel, CuiHelper.GetGuid());
            }
            public static void InputField(ref CuiElementContainer container, string panel, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter, string color = null)
            {
                container.Add(new CuiElement
                {
                    Name = "TestNameInput",
                    Parent = panel,
                    Components =
                    {
                                new CuiInputFieldComponent
                                {
                                   Text = text,
                                   CharsLimit = 50,
                                   
                                   Color = color,
                                   IsPassword = false,
                                   Command = command,
                                   Font = "robotocondensed-regular.ttf",
                                   FontSize = size,
                                   Align = align

                                },

                               new CuiRectTransformComponent
                               {
                                   AnchorMin = aMin,
                                   AnchorMax = aMax
                               }
                    }
                });
            }
        }



        #endregion Config UI

      

        #endregion Config

        #region SaveData

        private bool SaveButton(BaseEntity button,ulong playerid,BaseEntity signboard)
        {
            if (!cachedButtons.ContainsKey(button.net.ID.Value))
            {
                cachedButtons.Add(button.net.ID.Value, new ButtonInfo { Location = button.transform.position, Rotation = button.transform.rotation ,OwningPlayer = playerid,signboardID = signboard.net.ID.Value});
            }
            return true;
        }

        #endregion SaveData

        #region Classes
        class BTData
        {
            public Dictionary<ulong, ButtonInfo> Buttons = new Dictionary<ulong, ButtonInfo>();
           
        }

        class ButtonInfo
        {          
           public Vector3 Location; 
           public Quaternion Rotation;
           public ulong OwningPlayer;
           public ulong signboardID;
           
        }     

        #endregion Classes

        #region Data Management
        void SaveData()
        {
            var list = cachedButtons.Keys.ToList();
            foreach(var bt in list)
            {
                NetworkableId buttonid = new NetworkableId(ulong.Parse(bt.ToString()));
               
                Signage entity = Signage.serverEntities.Find(buttonid) as Signage;

                if (entity != null)
                {
                    FindOldButtonsAndKill(entity.transform.position);                   
                    entity.Kill();  
                }
                else
                {
                    cachedButtons.Remove(bt);   
                }
            }
            btData.Buttons = cachedButtons;           
            Button_Data.WriteObject(btData);
            Puts("ButtonData saved");
        }
        BasePlayer findPlayer(string name)
        {
            var player = BasePlayer.Find(name);
            if (player == null) return null;

            return player;
        }
        void SaveLoop() => timer.Once(900, () => { SaveData(); SaveLoop(); });
        void LoadData()
        {
            try
            {
                btData = Button_Data.ReadObject<BTData>();
                cachedButtons = btData.Buttons;               
            }
            catch
            {
                Puts("Couldn't load button data, creating new datafile");
                btData = new BTData();
            }
        }

        #endregion Data Management

        #region Lang

        private string GetLang(string key, BasePlayer player)
        {
            return lang.GetMessage(key, this)
                .Replace("{playername}", player.displayName)
                .Replace("{playerid}", player.UserIDString)
                .Replace("{playerlocation}", player.transform.position.ToString().Replace("(", "").Replace(")", ""))
                .Replace("{timeremaining}", getTimeRemain(player))
                .Replace("{cooldowntotal}", Settings.buttonSettings.buttonCooldown.ToString());
                
        }
        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["FallMessage"] = "Fell From 50meters High",
                ["SwimMessage"] = "Went for a Swim and found 3 sharks",
                ["ScarecrowMessage"] = "Found that Tinder only has Scarecrows",
                ["ScrapheliMessage"] = "Helicopters are Heavy . ACME style Murder",
                ["CactusMessage"] = "Found a Cactus and poke his way to heaven",
                ["C4Message"] = "Made a new Friend , C4",
                ["BearMessage"] = "Noticed that YogiBear is no fun in 3's",
                ["ScientistMessage"] = "Playing with A Scientist that loves him",
                ["LandmineMessage"] = "Needs to dance or DIE!!!!",
                ["RadiationMessage"] = "Bit a Nuclear Rod",
                ["SlapMessage"] = "Got Slapped so hard, his kids felt it",
                ["MlrsMessage"] = "could not hold on to that rocket",                
                ["ButtonCooldownMessage"] = "The Button is on a cooldown for you . {timeremaining} remaining from {cooldowntotal} Min's",
                ["Payback:RocketManMessage"] = "and found himself singing Rocketman from Elton John",
                ["Payback:SharkMessage"] = "will never order Sushi again",
                ["Payback:NakedMessage"] = "lost all his stuff",
                ["Payback:ShockerMessage"] = "had a shocking experience",
                ["Payback:ChickensMessage"] = "got a Winner,Winner and is now chicken dinner",
                ["Payback2:TrainMessage"] = "forgot to check for Trains",
                ["Payback2:F15Message"] = "is now a target from above",
                ["PVELordMessageLargeOil"] = "Became a PVE SweatLORD on Large Oil",
                ["PVELordMessageSmallOil"] = "Became a PVE SweatLORD on Small Oil",
                ["PVELordMessageCargo"] = "Became a PVE SweatLORD on Cargo",
                ["ShredderMessage"] = "Found his long lost CRUSH , The CarShredder!",
                ["DiarrheaMessage"] = "and will never eat Taco's again !!",
                ["GuntrapMessage"] = "and got stuck in a TRAP !!",
                ["FireBallMessage"] = "should never play with Matches!!!",
                ["EmbedTitle"] = "Red Button Pressed!",
                ["EmbedBody"] =
                    "**Player:**\n{playerName}\n{playerId}\n[Steam Profile](https://steamcommunity.com/profiles/{playerId})\n\n**Punishment Name:**\n{punName}\n\n**Punishmet Message**\n{punishment}\n\n",
                 ["BombVestMessage"] = "is about to go Boom!!!!!",


            }, this, "en");
        }

        #endregion Lang
    }
} 