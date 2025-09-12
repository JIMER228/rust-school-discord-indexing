using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch.Extend;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using WebSocketSharp;

namespace Oxide.Plugins
{
    [Info("Raid Block", "Hougan", "0.0.1")]
    public class RaidBlock : RustPlugin
    {
        #region Variables

        private static RaidBlock _;
        [PluginReference] private Plugin CustomRemove;
        private static List<RaidZone> Zones = new List<RaidZone>();

        #endregion
        
        #region Classes

        private class RaidUser : MonoBehaviour
        {
            public BasePlayer Player;
            public RaidZone Zone;

            public int ExtraCD = -1;
            public bool PrevZone = false;
            public string UIName = "UI_RaidBlock.Layer";

            public void Awake()
            {
                Player = GetComponent<BasePlayer>();
                InvokeRepeating(nameof(FindZone), 0f, 1f);
            }

            public void FindZone()
            {
                if (Zone != null)
                {
                    PrevZone = true;
                    if (Vector3.Distance(Player.transform.position, Zone.transform.position) > 60)
                    {
                        if (ExtraCD > 0)
                        {
                            ExtraCD--;
                            if (ExtraCD == 0)
                            {
                                
                                Zone = null;
                                Player.SendConsoleCommand($"note.inv 605467368 1 \"RAIDBLOCK ОКОНЧЕН\"");
                                PrevZone = false;
                                CuiHelper.DestroyUi(Player, UIName);
                                ExtraCD = 0;
                            }
                        }
                        else
                        {
							Player.SendConsoleCommand($"note.inv 605467368 1 \"ВЫ ВЫШЛИ ИЗ ЗОНЫ RB\"");
                            ExtraCD = Math.Min(Mathf.FloorToInt(Zone.RaidLeft), 30);
							Player.ChatMessage($"Вы покинули зону действия рейдблока.\n<size=10>Через некоторое время ограничения от рейдблока будут сняты.</size>");
                        }
                    }
                    else
                    {
                        ExtraCD = -1;
                    }

                    return;
                }

                if (PrevZone)
                {
                    PrevZone = false;
                    Player.SendConsoleCommand($"note.inv 605467368 1 \"RAIDBLOCK ОКОНЧЕН\"");
                }

                if (Player == null || !Player.IsConnected)
                {
                    Destroy(this);
                    return;
                }

                var zone = Zones.FirstOrDefault(p => Vector3.Distance(Player.transform.position, p.transform.position) < Configuration.RaidRadius);
                if (zone != null)
                {
                    TryBlock(zone); 
                }
            }

            public void TryBlock(RaidZone zone)
            {
                if (Zone != null) return;
                Zone = zone;
                
                ShowInterface();
                if (IsInvoking(nameof(UpdateInterface)))
                {
                    CancelInvoke(nameof(UpdateInterface));
                }
                InvokeRepeating(nameof(UpdateInterface), 0f, 1f);
            }
            
            public string TimeLeft()
            {
                TimeSpan ts = new TimeSpan();
                if (ExtraCD > 0)
                    ts = TimeSpan.FromSeconds(ExtraCD);
                else 
                    ts = TimeSpan.FromSeconds(Zone.RaidLeft);
                
                return string.Format("{0}:{1}", ts.Minutes.ToString("D2"), ts.Seconds.ToString("D2"));
            }
            
            public void UpdateInterface()
            {
                if (!Zone)
                {
                    return;
                }
                
                string countDown = TimeLeft();
                if (ExtraCD > 0 && ExtraCD < 10 || Zone.RaidLeft > 0 && Zone.RaidLeft < 10)
                {
                    Player.SendConsoleCommand($"note.inv 605467368 -1 \"RAIDBLOCK <color=white>{countDown}</color>\"");
                    return;
                }
                
                if (ExtraCD > 0)
                {
                    if (ExtraCD % 5 == 0)
                    {
                        Player.SendConsoleCommand($"note.inv 605467368 -5 \"RAIDBLOCK <color=white>{countDown}</color>\"");
                    }
                    return;
                }

                if (Zone.RaidLeft % 5 == 0)
                {
                    Player.SendConsoleCommand($"note.inv 605467368 -5 \"RAIDBLOCK <color=white>{countDown}</color>\"");
                }
            }

            public void ShowInterface()
            {
                if (!Zone)
                {
                    CuiHelper.DestroyUi(Player, UIName);
                    return;
                }
                
                Player.SendConsoleCommand($"note.inv 605467368 0 \"RAIDBLOCK НАЧАЛСЯ\"");
            }

            public void OnDestroy()
            {
                CuiHelper.DestroyUi(Player, UIName);
            }
        }
        
        private class RaidZone : MonoBehaviour
        {
            public float RaidLeft = Configuration.RaidLength;
            public List<BaseEntity> Blocks = new List<BaseEntity>();

            public void Awake()
            {
                InvokeRepeating(nameof(Decrease), 0f, 1f);
                
                Zones.Add(this);
            }
 
            public void Initialize(Vector3 position, BaseEntity entity)
            {
                transform.position = position; 
                 
                var list = new List<BuildingBlock>();
                Vis.Entities(position, 20f, list);
                
                var obj = entity.GetBuildingPrivilege(new OBB(entity.transform, entity.bounds)); 
                Interface.Oxide.LogWarning((obj == null).ToString());
 
                foreach (var check in list.Where(p => p.GetBuildingPrivilege() == obj)) 
                {
                    _.CustomRemove?.Call("BlockObject", check, $"ДОМ БЫЛ ПОДВЕРЖЕН РЕЙДУ");
                } 
            }

            public void UpdateRaid(BaseEntity entity)
            {
                RaidLeft = Configuration.RaidLength; 
                // TODO: SpawnEntity 
            } 

            public void Decrease()
            {
                RaidLeft--;
                
                if (RaidLeft <= 0) 
                {
                    Destroy(this);     
                }
            }

            public void OnDestroy()
            {
                if (Zones.Contains(this))
                    Zones.Remove(this);
                
                var list = new List<BaseEntity>();
                Vis.Entities(transform.position, 75f, list);   
                
                foreach (var check in list.Where(p => p.PrefabName.Contains("debris")))
                {
                    check.Kill();
                }
                
                Destroy(gameObject);
            }
        }
 
        private static class Configuration
        {
            public static float RaidLength = 300;
            public static float RaidRadius = 60;

            public static List<string> AllowedPrefabs = new List<string>
            {
                "autoturret", 
                "floor.ladder",
                "floor.frame",
                "floor.grill",
                "wall.frame",
                "wall.window",
                "mining",
                "external",
                "workbench3",
                "workbench2",
				"shutter.metal.embrasure",
				"electric.windmill.small",
				"elevator",
                "window" 
            };
        }
        object OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
            var result = IsInRaid(player);
            if (result)
            {
                SendReply(player, $"Вы не можете <color=orange>чинить объекты</color> во время рейда!");
                return true;
            } 

            return null;
        }

        [ConsoleCommand("getraids")]
        private void CmdConsoleRaids(ConsoleSystem.Arg args)
        {
            if (args.Player() != null) return;
             
            PrintError($"Raid amount: {Zones.Count} r.");
        }
        
        object CanBuild(Planner plan, Construction prefab)
        {
            var player = plan.GetOwnerPlayer();  
            var ent = plan.GetEntity();
            if (!IsInRaid(player)) return null;
            
            if (prefab.fullName.Contains("window") && prefab.fullName != "assets/prefabs/building core/wall.window/wall.window.prefab")
            { 
                player.ChatMessage($"Ставить окна во время <color=#e6533d>рейд блока</color> запрещено.");
                return true;
            }
			
			if (prefab.fullName.Contains("door.hinged.toptier") && prefab.fullName != "assets/prefabs/building core/wall.doorway/wall.doorway.prefab")
            { 
                player.ChatMessage($"Ставить МВК двери во время <color=#e6533d>рейд блока</color> запрещено.");
                return true;
            }
			
			if (prefab.fullName.Contains("door.double.hinged.toptier") && prefab.fullName != "assets/prefabs/building core/wall.frame/wall.frame.prefab")
            { 
                player.ChatMessage($"Ставить МВК двери во время <color=#e6533d>рейд блока</color> запрещено");
                return true;
            }
			
			if (prefab.fullName.Contains("wall.frame.garagedoor") && prefab.fullName != "assets/prefabs/building core/wall.frame/wall.frame.prefab")
            { 
                player.ChatMessage($"Ставить гаражку во время <color=#e6533d>рейд блока</color> запрещено.");
                return true;
            }
			
			if (prefab.fullName.Contains("wall.frame.shopfront.metal") && prefab.fullName != "assets/prefabs/building core/wall.frame/wall.frame.prefab")
            { 
                player.ChatMessage($"Ставить витрину во время <color=#e6533d>рейд блока</color> запрещено.");
                return true;
            }

            return null;
        }
		

        private bool IsInRaid(BasePlayer player) => player.GetComponent<RaidUser>()?.Zone != null;
        private bool IsInRaid(Vector3 pos) => Zones.Any(p => Vector3.Distance(p.transform.position, pos) < Configuration.RaidRadius);
        
        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            _ = this;
            
            timer.Once(1f, () => { BasePlayer.activePlayerList.ToList().ForEach(OnPlayerConnected); });
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            var obj = player.GetComponent<RaidUser>();
            if (obj != null)
            {
                obj.ShowInterface();
                return;
            }  
            
            player.gameObject.AddComponent<RaidUser>().ShowInterface();
        }

        private void Unload()
        {
            DestroyAll<RaidZone>();
            DestroyAll<RaidUser>();
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null || !entity.PrefabName.Contains("debris")) return;
            
            if (info.damageTypes.GetMajorityDamageType() == DamageType.Explosion)
                info.damageTypes.ScaleAll(0); 
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            if (!entity.PrefabName.Contains("debris.wall")) return;
            
            var raid = Zones.FirstOrDefault(p => Vector3.Distance(entity.transform.position, p.transform.position) < Configuration.RaidRadius);
            if (raid == null) entity.Kill(); 
        }
        
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            var raid = Zones.FirstOrDefault(p => Vector3.Distance(entity.transform.position, p.transform.position) < Configuration.RaidRadius);
            if (raid == null)
            {
                if (info != null && info.damageTypes.GetMajorityDamageType() == DamageType.Decay) return;
                
                if (info == null || info.Initiator == null || !(info.Initiator is BasePlayer) || !IsEntityBlocked(entity)) return;
                var rb = entity.GetBuildingPrivilege(new OBB(entity.transform, entity.bounds)); 
                if (rb != null && rb.IsAuthed(info.InitiatorPlayer)) return;  
                
                GameObject obj = new GameObject();
                obj.AddComponent<RaidZone>().Initialize(entity.transform.position, entity); 
            }
            else
            {
                if (!IsEntityBlocked(entity)) return; 
                
                raid.UpdateRaid(entity);
            }  
  
            if (!entity.PrefabName.Contains("wall.half") && !entity.PrefabName.Contains("wall.doorway")) return;
            
            var pos = entity.transform.position.ToString();
            var rot = entity.transform.rotation;   
            
            timer.Once(0.5f, () =>
            {
                var ent = GameManager.server.CreateEntity("assets/prefabs/debris/debris.wall.prefab", pos.ToVector3() + new Vector3(0, 0, 0), rot);

                UnityEngine.Object.Destroy(ent.GetComponent<GroundWatch>());
                ent.Spawn();
            });
        }   
        
        private bool? CanAffordUpgrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
        {
            if (Zones.Any(p => Vector3.Distance(player.transform.position, p.transform.position) < Configuration.RaidRadius))
            {
                if (grade == BuildingGrade.Enum.Wood) return null;

                var obj = player.GetComponent<RaidUser>();
                obj?.UpdateInterface();
                
                player.ChatMessage($"Во время рейда, вы можете улучшать только <color=orange>в дерево</color>");
                return false;
            }
            
            return null;
        } 
        
        #endregion

        #region Utils
        
        private bool IsEntityBlocked(BaseCombatEntity entity)
        {
            if (entity is BuildingBlock)
            {
                if (((BuildingBlock) entity).grade == BuildingGrade.Enum.Twigs) return false;
                return true;
            }
            else if (entity is Door)
            {
                return true;
            }

            var prefabName = entity.ShortPrefabName;
            var result     = false;
            
            foreach (string p in Configuration.AllowedPrefabs)
            {
                if (prefabName.Contains(p))
                {
                    return true;
                }
            }  

            return false;
        }

        private void DestroyAll<T>()
        {
            var objects = UnityEngine.Object.FindObjectsOfType(typeof(T));
            objects?.ToList().ForEach(UnityEngine.Object.Destroy); 
        }

        #endregion
    }
}