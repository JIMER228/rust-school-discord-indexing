using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Egg Tc", "Magic Services // Zeeuss // Shady14u", "1.0.3")]
    [Description("Adds a small wooden box on top of a tool cupboard with a command. Which contains a Golden EGG.")]
    class EggTc : RustPlugin
    {

        #region Init & Fields

        readonly Dictionary<NetworkableId, Timer> _timerDic = new();
        private const string UsePerms = "eggtc.use";
       
        void Init()
        {
            AddCovalenceCommand("eggtc", "EggCmd", UsePerms);      
            LoadConfigVariables();
        }
        #endregion

        #region Command

        void EggCmd(IPlayer player, string command, string[] args)
        {
            var bPlayer = player.Object as BasePlayer;
            if(bPlayer == null) return;
            if(Physics.Raycast(bPlayer.eyes.HeadRay(), out var hit, 15f, Layers))
            {
                if(hit.GetEntity() != null)
                {
                    var ent = hit.GetEntity();
                    if(ent is BuildingPrivlidge privlidge)
                    {
                        TryAddBox(privlidge, true, player);
                        return;
                    }
                    player.Message(lang.GetMessage("MustLook", this, player.Id));
                    return;
                }

            }
            player.Message(lang.GetMessage("MustLook", this, player.Id));
            return;

        }
        #endregion

        #region Data handle
       
        void Unload()
        {
            foreach(var tmr in _timerDic.Values)
            {
                if(tmr is {Destroyed: false}) tmr.Destroy();
            }
        }
        #endregion

        #region Oxide hooks

        object CanPickupEntity(BasePlayer player, BoxStorage entity)
        {
            if (!IsEggBox(entity)) return null;
            SendReply(player, lang.GetMessage("CantPick", this, player.UserIDString));
            return false;

        }
        
        void OnEntityDeath(BoxStorage entity, HitInfo info)
        {
            try
            {
                if (!IsEggBox(entity)) return;
                
                var ent = entity.GetParentEntity();

                if (ent == null) return;
                
                var bd = ent as BuildingPrivlidge;
                if(bd == null) return;
                timer.Once(0.1f, () =>
                { 
                    TryAddBox(bd, false);
                });
            }
            catch (Exception ex)
            {
                Puts($"[EggTc] Error {ex.Message}");
            }

        }
      
        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (container.entityOwner == null) return;
            if (!IsEggBox(container.entityOwner)) return;
            var tmr = timer.Once(_configData.RespawnSeconds, () =>
            {
                try
                {
                    var itm = ItemManager.CreateByItemID(-1002156085, 1);
                    itm.MoveToContainer(container, 0);
                }
                catch (Exception ex)
                {
                    Puts($"[EggTc] Error: {ex.Message}");
                }
            });
            _timerDic[container.entityOwner.net.ID] = tmr;
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            try
            {
                if (container.entityOwner == null) return;
                if (!IsEggBox(container.entityOwner)) return;
                if (_timerDic.TryGetValue(container.entityOwner.net.ID, out var eggTimer))
                {
                    eggTimer.Destroy();
                }
            }
            catch (Exception ex)
            {
                Puts($"[EggTc] Error: {ex.Message}");
            }
        }
        
        ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            try
            {
                if (container.entityOwner != null)
                {
                    if (!IsEggBox(container.entityOwner)) return null;

                    if (item.info.itemid != -1002156085) return ItemContainer.CanAcceptResult.CannotAccept;
                    
                    if (targetPos != 0)
                    {
                        return ItemContainer.CanAcceptResult.CannotAccept;
                    }
                    if (item.amount > 1)
                    {
                        return ItemContainer.CanAcceptResult.CannotAccept;
                    }

                    if (container.GetSlot(0) == null) return ItemContainer.CanAcceptResult.CanAccept;
                    return container.GetSlot(0).amount >= 0 ? ItemContainer.CanAcceptResult.CannotAccept : ItemContainer.CanAcceptResult.CanAccept;
                }
            }
            catch (Exception ex)
            {
                Puts($"[EggTc] ErrorCanAccept: {ex.Message}");
            }
            return null;
        }

        void OnServerInitialized(bool initial)
        {
            var boxes = BaseNetworkable.serverEntities.OfType<BoxStorage>().Where(IsEggBox).ToList();
            foreach (var boxStorage in boxes)
            {
                if (boxStorage.inventory.itemList.Count != 0) continue;
                
                var tmr = timer.Once(_configData.RespawnSeconds, () =>
                {
                    if (boxStorage.IsDestroyed) return;
                    var itm = ItemManager.CreateByItemID(-1002156085, 1);
                    itm.MoveToContainer(boxStorage.inventory, 0);
                });
                _timerDic[boxStorage.net.ID] = tmr;
            }
        }

        #endregion
        
        #region Helpers

        static bool IsEggBox(BaseEntity box)
        {
            try
            {
                if (box == null || box is not BoxStorage storage) return false;

                if (storage.inventory.capacity != 1) return false;
                var parent = storage.GetParentEntity() as BuildingPrivlidge;
                
                return parent != null;
            }
            catch (Exception ex)
            {
                return false;
            }
            
        }

        void TryAddBox(BuildingPrivlidge tc, bool spawnEgg, IPlayer player = null)
        {
            if(tc == null) return;
            var box = tc.children.FirstOrDefault(x => x.GetEntity() as BoxStorage);
            
            if(box == null)
            {
                var smallBox = GameManager.server.CreateEntity("assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab", 
                    tc.transform.position + new Vector3(0, 1.85f, 0), tc.transform.rotation) as BoxStorage;

                if (smallBox == null) return; 
                smallBox.Spawn();
                smallBox.inventory.capacity = 1;
                smallBox.SetParent(tc,true,true);
                
                if (spawnEgg)
                {
                    var egg = ItemManager.CreateByItemID(-1002156085, 1);
                    egg.MoveToContainer(smallBox.inventory, 0);
                    if (_timerDic.ContainsKey(smallBox.net.ID))
                    {
                        _timerDic[smallBox.net.ID].Destroy();
                        _timerDic.Remove(smallBox.net.ID);
                    }
                }else
                {
                    var tmr = timer.Once(_configData.RespawnSeconds, () =>
                    {
                        if (smallBox.IsDestroyed) return;
                        var egg1 = ItemManager.CreateByItemID(-1002156085);
                        egg1.MoveToContainer(smallBox.inventory, 0);
                    });
                    _timerDic[smallBox.net.ID] = tmr;
                }

                player?.Message(lang.GetMessage("BoxAdded", this, player.Id));

            }
            else
            {
                box.SetParent(null, true, true);
                box.Kill();

                player?.Message(lang.GetMessage("Removed", this, player.Id));
            }
        }

        private static readonly int Layers = LayerMask.GetMask("Construction", "Deployed");
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //FORMAT: {0} = storage short prefab name, {1} = seconds to unlock
                ["MustLook"] = "You must look at TC",
                ["BoxAdded"] = "Box added to TC!",
                ["Removed"] = "Box removed from this TC!",
                ["CantPick"] = "You can't pick up EGG box!",



            }, this);
        }
        #endregion

        #region Config
        private ConfigData _configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "EGG Respawn Timer (seconds)")]
            public float RespawnSeconds = 60f;

        }

        private void LoadConfigVariables()
        {
            try
            {
                _configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
                _configData = new ConfigData();
                return;
            }
            SaveConfig(_configData);
            return;
        }

        protected override void LoadDefaultConfig()
        {
            _configData = new ConfigData();
            SaveConfig(_configData);
        }

        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion

    }
}
