using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Custom Remove", "Hougan", "0.0.1")]
    public class CustomRemove : RustPlugin
    {
        private class RemovableObject
        {
            [JsonProperty("S")]
            public bool IsInteractable;
            [JsonProperty("T")]
            public long BuildTime; 
            [JsonIgnore]
            public long LastAttackedTime;

            public string Label() => IsInteractable ? $"<size=14>РЕМУВ/ПОВОРОТ ДОСТУПЕН</size>\n" : $"<size=14>РЕМУВ/ПОВОРОТ ОТНЫНЕ НЕ ДОСТУПЕН</size>\n";

            public string InteractStatus(bool admin = false)
            {
                if (!IsInteractable)
                {
                    if (BuildTime < 0)
                        return $"{Label()}"  + "<size=13>ИСТЕКЛО ВРЕМЯ УДАЛЕНИЯ</size>\n<color=#d3c9c0>✕</color>";
                    
                    return $"{Label()}" + "<size=13>ДОМ БЫЛ ПОДВЕРЖЕН РЕЙДУ</size>\n<color=#d3c9c0>✕</color>";
                }
                
                if (CurrentTime() >= (BuildTime + RemoveTime))  
                {
                    IsInteractable = false;
                    BuildTime = -1;  

                    return InteractStatus(); 
                }

                var totalTime = TimeSpan.FromSeconds((BuildTime + RemoveTime) - CurrentTime());
                
                return $"<size=18>{Label()}</size>" +
                        $"<size=16>{totalTime.Hours}ч. {totalTime.Minutes}м.</size>";
            }

            public string BuildTimeConverted()
            {
                var date = DateTime.FromFileTime(BuildTime);
                return $"{date.ToShortDateString()} {date.ToShortTimeString()}";
            }

            public static RemovableObject Generate(BuildingBlock obj)
            {
                return new RemovableObject
                {
                    IsInteractable = true,
                    BuildTime = (long) CurrentTime()
                }; 
            }
        }

        private static Dictionary<uint, RemovableObject> RemovableInfos = new Dictionary<uint,RemovableObject>();
        private static int RemoveTime = 21600;
        private static CustomRemove _;

        private void OnServerInitialized()
        {
            _ = this;

            if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name))
                RemovableInfos = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<uint, RemovableObject>>(Name);

            var dirty = new List<uint>();
            foreach (var check in RemovableInfos.Where(p => BaseNetworkable.serverEntities.Find(p.Key)?.IsDestroyed ?? true))
                dirty.Add(check.Key);

            foreach (var check in dirty)
                RemovableInfos.Remove(check);
            
            SaveData();
            
            UnityEngine.Object.FindObjectsOfType<BuildingBlock>().ToList().ForEach(p =>
            {
                if (!RemovableInfos.ContainsKey(p.net.ID))
                    RemovableInfos.Add(p.net.ID, RemovableObject.Generate(p));
                
                p.SetFlag(BaseEntity.Flags.Reserved1, true);
                p.SetFlag(BaseEntity.Flags.Reserved2, true);
                p.CancelInvoke(p.StopBeingRotatable);
                p.CancelInvoke(p.StopBeingDemolishable); 
            });

            timer.Every(600, SaveData);
        }

        private void Unload() => SaveData();

        private void SaveData()
        {
            PrintWarning($"Saving removable information");
            Interface.Oxide.DataFileSystem.WriteObject(Name, RemovableInfos);
        }

        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (info == null || !player.IsBuildingAuthed()) return;
            
            var obj = info.HitEntity as BuildingBlock;
            if (obj == null) return;

            RemovableObject removeInfo;
            if (!RemovableInfos.TryGetValue(obj.net.ID, out removeInfo) || CurrentTime() < removeInfo.LastAttackedTime + 10)
            {
                return;
            }
            
            SendMarker(player, info.PointEnd, removeInfo.InteractStatus());
            removeInfo.LastAttackedTime = (long) CurrentTime();
        }

        private void BlockObject(BuildingBlock obj, string reason)
        {
            if (!RemovableInfos.ContainsKey(obj.net.ID))
                RemovableInfos.Add(obj.net.ID, RemovableObject.Generate(obj));

            var info = RemovableInfos[obj.net.ID];
            info.IsInteractable = false;
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            var obj = entity as BuildingBlock;
            if (obj == null) return; 
            
            if (!RemovableInfos.ContainsKey(obj.net.ID))
                RemovableInfos.Add(obj.net.ID, RemovableObject.Generate(obj));
            
            NextTick(() =>
            {
                obj.CancelInvoke(obj.StopBeingRotatable);
                obj.CancelInvoke(obj.StopBeingDemolishable);
            });
        }
        
        private static void SendMarker(BasePlayer player, Vector3 position, string text)
        {
            player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true); 
                    
            player.SendEntityUpdate();   
            player.SendConsoleCommand("ddraw.text", 2f, Color.white, position, text);
                    
            if (player.Connection.authLevel < 2)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);  
                player.SendConsoleCommand("camspeed 0");
            }
                    
            player.SendEntityUpdate();
			
        }
        
        private object OnStructureDemolish(BuildingBlock block, BasePlayer player)
        { 
            RemovableObject removeInfo;
            if (RemovableInfos.TryGetValue(block.net.ID, out removeInfo))
            {
                var result = removeInfo.InteractStatus();
                if (!removeInfo.IsInteractable)
                {
                    RaycastHit info;
                    Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out info);

                    if (CurrentTime() < removeInfo.LastAttackedTime + 3)
                    {
                        return false; 
                    }
                    SendMarker(player, info.point, result); 
                    return false; 
                }
            }
            
            return null;
        }

        private object OnStructureRotate(BuildingBlock block, BasePlayer player)
        { 
            RemovableObject removeInfo;
            if (RemovableInfos.TryGetValue(block.net.ID, out removeInfo))
            {
                RaycastHit info;
                Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out info);
                
                var result = removeInfo.InteractStatus();
                if (!removeInfo.IsInteractable)
                {
                    if (CurrentTime() < removeInfo.LastAttackedTime + 3)
                    {
                        return false; 
                    }
                    SendMarker(player, info.point, result); 
                    return false;
                }   
            }
            
            return null;
        }
        
        private static double CurrentTime() => DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;  
    }
}