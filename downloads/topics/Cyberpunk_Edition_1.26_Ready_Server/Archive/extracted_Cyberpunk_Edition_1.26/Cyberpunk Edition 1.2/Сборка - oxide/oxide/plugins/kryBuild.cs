using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins 
{
    /// <summary>
    /// 1.0.1
    /// - fixed bug, when some bblocks not removes
    /// 
    /// 1.0.2
    /// - added support for integrated in rust teams
    /// </summary>
    [Info ("kryBuild", "xkrystalll", "1.0.5")]
    class kryBuild : RustPlugin 
    {
        #region Fields
        [PluginReference] Plugin ImageLibrary;
        public static kryBuild Instance;
        private static Dictionary<string, int> deployedToItem = new Dictionary<string, int>();
        private List<string> restrictedPrefabs = new List<string>()
        {
            "rowboat",
            "rhib",
            "minicopter",
            "helicopter",
            "transportheli",
            "scrap-transport",
            "horse",
            "boar",
            "stag"
        };
        private bool IsRestricted(string prefab)
        {
            foreach (var x in restrictedPrefabs)
            {
                if (prefab.Contains(x))
                    return true;
            }
            return false;
        }
        private const string Layer = "ui.kryBuild.bg";
        
        #endregion
        
        #region Classes
        private class kryPlayer : FacepunchBehaviour
        {
            public static Dictionary<BasePlayer, kryPlayer> Players = new Dictionary<BasePlayer, kryPlayer>();
            public BasePlayer _player;
            private int _grade;
            private string _mode;
            private string _maymode;
            public void Awake()
            {
                var attachedPlayer = GetComponent<BasePlayer>();
                if ( attachedPlayer == null || !attachedPlayer.IsConnected )
                {
                    return;
                }

                _player = attachedPlayer;
                Players[_player] = this;
            }
            public void SetGrade(int newGrade) => _grade = newGrade;
            public void SetMode(string mode) => _mode = mode;
            public void SetMayMode(string mode) => _maymode = mode;
            public string GetMayMode() => _maymode;
            public string GetMode() => _mode;
            public int GetGrade() => _grade;
            public void Destroy() => Destroy(this);
        }
        #endregion

        #region Hooks
        void OnEntityBuilt(Planner plan, GameObject go)
        {
            BasePlayer p = plan?.GetOwnerPlayer();
            kryPlayer player = GetPlayer(p);
            if (player.GetMode() != "upgrade") return;

            BuildingBlock target = go.GetComponent<BuildingBlock>();
            if (TakeResources(p, player.GetGrade(), target))
            {
                target.SetGrade((BuildingGrade.Enum)player.GetGrade());
                UpdateBuildingBlock(target);
            }
            else 
            {
                p.ChatMessage("<size=18>У вас <color=red>не хватает</color> ресурсов!</size>");
            }
        }
        private void LoadImages()
        {
            ImageLibrary.Call("AddImage", "https://imgur.com/pLWKmoo.png", "grade_1");
            ImageLibrary.Call("AddImage", "https://imgur.com/GKOrspF.png", "grade_2");
            ImageLibrary.Call("AddImage", "https://imgur.com/7eUfR5Z.png", "grade_3");
            ImageLibrary.Call("AddImage", "https://imgur.com/ij5WIzl.png", "grade_4");
            ImageLibrary.Call("AddImage", "https://imgur.com/QQe2LFC.png", "grade_5");
            // ImageLibrary.Call("");
        }
        private void OnServerInitialized()
        {
            if (!ImageLibrary)
            {
                PrintError("Plugin ImageLibrary not founded! Plugin not work.");
                PrintError("Plugin ImageLibrary not founded! Plugin not work.");
                PrintError("Plugin ImageLibrary not founded! Plugin not work.");
                return;
            }

            // LoadConfig();
            LoadImages();
            Instance = this;
            List<ItemDefinition> ItemsDefinition = ItemManager.GetItemDefinitions() as List<ItemDefinition>;
            foreach (ItemDefinition itemdef in ItemsDefinition)
            {
                if (itemdef?.GetComponent<ItemModDeployable>() == null) continue;
                if (deployedToItem.ContainsKey(itemdef.GetComponent<ItemModDeployable>().entityPrefab.resourcePath)) continue;
                deployedToItem.Add(itemdef.GetComponent<ItemModDeployable>().entityPrefab.resourcePath, itemdef.itemid);
            }
            BasePlayer.activePlayerList.ToList().ForEach(AddPlayerInfo);
        }
        private void Unload()
        {
            BasePlayer.activePlayerList.ToList().ForEach(TriggerUnload);
        }
        object OnHammerHit(BasePlayer attacker, HitInfo info)
        {
            bool? result = null;
            if (info == null || attacker == null) return null;
            BuildingBlock bblock = info.HitEntity as BuildingBlock;
            BaseEntity hitEntity = info.HitEntity as BaseEntity;
            if (hitEntity == null) 
                return null;
            if (hitEntity.OwnerID == 0) 
                return null;
            kryPlayer player = GetPlayer(attacker);
            if (player == null) 
                return null;
            NextTick(() => 
            {
                try
                {

                    // Puts("1");
                    if (player.GetMode() == null)
                        return;
                    if (player.GetMode() == "remove")
                    {
                        // Puts("2");
                        if (hitEntity.OwnerID == player._player.userID || 
                        (cfg.useTeams && cfg.canTeammatesDeleteBuildings && player._player.Team.members.Any(x => hitEntity.OwnerID == x)) 
                        && !IsRestricted(hitEntity.PrefabName))
                        {
                            // Puts("3");
                            StorageContainer storage = info.HitEntity as StorageContainer;
                            if (storage)
                            {
                                // Puts("4");
                                if (storage.inventory.itemList.Count > 0) for (int i = storage.inventory.itemList.Count - 1;
                                i >= 0;
                                i--)
                                {
                                    var item = storage.inventory.itemList[i];
                                    if (item == null) continue;
                                    if (item.info.shortname == "water") continue;
                                    float single = 20f;
                                    Vector3 vector32 = Quaternion.Euler(UnityEngine.Random.Range(-single * 0.1f, single * 0.1f), UnityEngine.Random.Range(-single * 0.1f, single * 0.1f), UnityEngine.Random.Range(-single * 0.1f, single * 0.1f)) * Vector3.up;
                                    BaseEntity baseEntity = item.Drop(storage.transform.position + (Vector3.up * 0f), vector32 * UnityEngine.Random.Range(5f, 10f), UnityEngine.Random.rotation);
                                    baseEntity.SetAngularVelocity(UnityEngine.Random.rotation.eulerAngles * 5f);
                                }
                                
                                Item x = ItemManager.CreateByItemID(deployedToItem[storage.PrefabName], 1);
                                attacker.GiveItem(x, BaseEntity.GiveItemReason.PickedUp);
                                // Puts("5");
                            }
                            else 
                            {
                                // Puts("6");
                                GiveRefund(hitEntity as BaseCombatEntity, attacker);
                            }
                            hitEntity.Kill(BaseNetworkable.DestroyMode.Gib);
                            result = false;
                        }
                    }
                    else if (player.GetMode() == "upgrade" || (cfg.useTeams && player._player.Team.members.Any(x => hitEntity.OwnerID == x)))
                    {
                        // Puts("7");
                        if (hitEntity.OwnerID == player._player.userID)
                        {
                            // Puts("8");
                            if (bblock == null)
                                return;
                            if ((int)bblock.grade >= player.GetGrade()) { return; }
                            // Puts("9");
                            if (TakeResources(attacker, player.GetGrade(), bblock))
                            {
                                // Puts("10");
                                bblock.SetGrade((BuildingGrade.Enum)player.GetGrade());
                                // Puts("11");
                                UpdateBuildingBlock(bblock);
                                // Puts("12");
                                result = false;
                            }
                            else { attacker.ChatMessage("<size=18>У вас <color=red>не хватает</color> ресурсов!</size>"); }
                        }
                        // Puts("13");
                    }
                }
                catch {}
            });
            return result;
            
        }
        
        private string FixNames(string name)
        {
            switch (name)
            {
                case "wall.external.high.wood": return "wall.external.high";
                case "electric.windmill.small": return "generator.wind.scrap";
                case "graveyardfence": return "wall.graveyard.fence";
                case "coffinstorage": return "coffin.storage";
            }
            return name;
        }

        private bool GiveRefund(BaseCombatEntity entity, BasePlayer player)
        {
            var name = entity.ShortPrefabName;
            name = Regex.Replace(name, "\\.deployed|_deployed", "");
            name = FixNames(name);

            var item = ItemManager.CreateByName(name);
            if (item != null)
            {
                player.inventory.GiveItem(item);
                return true;
            }

            if (entity != null)
            {
                var cost = entity.BuildCost();
                if (cost != null)
                {
                    foreach (var value in cost)
                    {
                        var x = ItemManager.Create(value.itemDef, Convert.ToInt32(value.amount));
                        if (x == null) { continue; }
                        player.GiveItem(x);
                    }

                    return true;
                }

                return false;
            }

            return false;
        }


        object OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
            kryPlayer kryPlayer = GetPlayer(player);
            if (kryPlayer.GetMode() != "null")
                return false;
            return null;
        }
        void OnActiveItemChanged(BasePlayer p, Item oldItem, Item newItem)
        {
            DestroyUI(p);
            if (p.IsNpc || p == null || !p.UserIDString.StartsWith("7656") || !p.IsConnected) return;
            kryPlayer player = GetPlayer(p);
            if (newItem == null) 
            {
                player.SetMode("null");
                player.SetMayMode("null");
                return; 
            }
            // if (p == null) return; 
            if (newItem.info.itemid != 200773292 && newItem.info.itemid != 1525520776)
            {
                player.SetMode("null");
                player.SetMayMode("null");
            }
            if (newItem.info.itemid == 200773292)
            {
                player.SetMayMode("remove/upgrade");
            }
            else if (newItem.info.itemid == 1525520776)
            {
                player.SetMayMode("upgrade");
            }
            player.SetGrade(0);
        }
        void OnPlayerInput(BasePlayer player, InputState input)
        {
            kryPlayer kryPlayer = GetPlayer(player);
            if (kryPlayer == null)
                return;
            if (kryPlayer.GetMayMode() == null)
                return;
            if (string.IsNullOrEmpty(kryPlayer.GetMayMode()))
                return;
            if (input.WasJustPressed(BUTTON.RELOAD) && (bool)kryPlayer?.GetMayMode().Contains("remove"))
            {
                cmdRemove(player);
            }
            else if (input.WasJustPressed(BUTTON.USE) && (bool)kryPlayer?.GetMayMode().Contains("up"))
            {
                cmdUp(player, "", new string[] {});
            }
        }
        private void OnDebugCheckCalled()
        {
#if DEBUG
            PrintWarning($"{Title} - debug mode: ON");
#endif
        }
        void OnPlayerConnected(BasePlayer p) => AddPlayerInfo(p);
        #endregion
        
        #region Methods
        private bool TakeResources(BasePlayer player, int playerGrade, BuildingBlock buildingBlock)
        {
            var itemsToTake = new Dictionary<int, int>();

            foreach (var itemAmount in buildingBlock.blockDefinition.grades[playerGrade].costToBuild)
            {
                if (!itemsToTake.ContainsKey(itemAmount.itemid))
                {
                    itemsToTake.Add(itemAmount.itemid, 0);
                }

                itemsToTake[itemAmount.itemid] += (int)itemAmount.amount;
            }

            var canAfford = true;
            foreach (var itemToTake in itemsToTake)
            {
                if (!HasItemAmount(player, itemToTake.Key, itemToTake.Value))
                {
                    canAfford = false;
                    break;
                }
            }
            if (canAfford)
            {
                foreach (var itemToTake in itemsToTake)
                {
                    TakeItem(player, itemToTake.Key, itemToTake.Value);
                }
            }
            return canAfford;
        }
        private void AddResourses(BasePlayer player, int playerGrade, BuildingBlock buildingBlock)
        {
            Puts((buildingBlock == null).ToString());
            var itemsToAdd = new Dictionary<int, int>();
            foreach (var itemAmount in buildingBlock.blockDefinition.grades[(int)buildingBlock.grade].costToBuild)
            {
                if (!itemsToAdd.ContainsKey(itemAmount.itemid))
                {
                    itemsToAdd.Add(itemAmount.itemid, 0);
                }

                itemsToAdd[itemAmount.itemid] += (int)itemAmount.amount;
            }
            foreach (var item in itemsToAdd)
            {
                AddItem(player, item.Key, item.Value / 2);
            }
        }
        public static void TakeItem(BasePlayer player, int itemId, int itemAmount)
        {
            if (player.inventory.Take(null, itemId, itemAmount) > 0)
            {
                player.SendConsoleCommand("note.inv", itemId, itemAmount * -1);
            }
        }
        public void AddItem(BasePlayer p, int itemId, int itemAmount)
        {
            Item itemToRefund = ItemManager.CreateByItemID(itemId, itemAmount);
            p.GiveItem(itemToRefund, BaseEntity.GiveItemReason.ResourceHarvested);
        }
        public static bool HasItemAmount(BasePlayer player, int itemId, int itemAmount)
        {
            var count = 0;
            foreach (var item in player.inventory.AllItems())
            {
                if (item.info.itemid == itemId)
                {
                    count += item.amount;
                }
            }

            return count >= itemAmount;
        }
        private void DetachGrade(BasePlayer p)
        {
            if (p == null || !p.IsConnected)
                return;
            DestroyUI(p);
            kryPlayer kryPlayer = GetPlayer(p);
            if (kryPlayer == null)
                return;
            kryPlayer?.SetMode("null");
            if (p.GetActiveItem()?.info?.itemid == 200773292)
            {
                kryPlayer?.SetMayMode("upgrade/remove");
            }
            else
            {
                kryPlayer?.SetMayMode("upgrade");
            }

        }
        private void UpdateBuildingBlock(BuildingBlock target)
        {
            target.SetHealthToMax();
            target.StartBeingRotatable();
            target.SendNetworkUpdate();
            target.UpdateSkin();
            target.ResetUpkeepTime();
            target.GetBuilding()?.Dirty();
        }
        private void DestroyAll<T>() where T : MonoBehaviour
        {
            foreach (var type in UnityEngine.Object.FindObjectsOfType<T>())
            {
                UnityEngine.Object.Destroy(type);
            }
        }
        private void DestroyUI(BasePlayer p) => CuiHelper.DestroyUi(p, Layer);
        private void UI_DrawImage(BasePlayer p, int grade)
        {
            if (p == null)
                return;
            if (grade < 0 || grade > 5)
                return;
            DestroyUI(p);
            var container = new CuiElementContainer();
            container.Add(new CuiElement()
            {
                Name = Layer,
                Parent = "Overlay",
                Components = 
                {
                    new CuiRawImageComponent() { Png = (string)ImageLibrary.Call("GetImage", $"grade_{grade}") },
                    new CuiRectTransformComponent() { AnchorMin = "0.65375 -0.01555556", AnchorMax = "0.79625 0.15" }
                }
            });
            CuiHelper.AddUi(p, container);
        }
        private void TriggerUnload(BasePlayer p)
        {
            Instance = null;
            DestroyAll<kryPlayer>();
            kryPlayer.Players.Clear();
            DestroyUI(p);
        }
        private void AddPlayerInfo(BasePlayer p)
        {
            kryPlayer kryPlayer;
            if ( !kryPlayer.Players.TryGetValue(p, out kryPlayer) )
            {
                kryPlayer = p.gameObject.AddComponent<kryPlayer>();
                kryPlayer.SetGrade(0);
                kryPlayer.SetMode("null");
                kryPlayer.SetMayMode("null");
            }
        }
        private kryPlayer GetPlayer(BasePlayer p) => kryPlayer.Players.GetValueOrDefault(p);
        #endregion

        #region Commands
        [ChatCommand("up")]
        void cmdUp(BasePlayer p, string command, string[] args)
        {
            if (p == null)
                return;
            kryPlayer kryPlayer = GetPlayer(p);
            if (kryPlayer == null)
                return;
            int grade = kryPlayer.GetGrade();
            if (grade < 0)
            {
                grade = 0;
                kryPlayer.SetGrade(0);
            }
            if (grade >= 4) { grade = 0; kryPlayer.SetGrade(0); DetachGrade(p); return; }
            if (args.IsNullOrEmpty())
            {
                grade++;
            }
            else
            {
                int newGrade;
                if (int.TryParse(args[0], out newGrade))
                {
                    grade = newGrade;
                }
            }
            kryPlayer?.SetGrade(grade);
            kryPlayer?.SetMode("upgrade");
            UI_DrawImage(p, grade);
        }
        [ChatCommand("remove")]
        void cmdRemove(BasePlayer p)
        {
            kryPlayer kryPlayer = GetPlayer(p);
            if (kryPlayer.GetMode() == "remove")
            {
                DestroyUI(p);
                kryPlayer.SetMode("null");
                return;
            }
            kryPlayer.SetGrade(0);
            kryPlayer.SetMode("remove");
            UI_DrawImage(p, 5);
        }
        #endregion
    
        #region Config
        private ConfigData cfg;
        public class ConfigData
        {
            [JsonProperty("Использовать встроенные тимы раста?")]
            public bool useTeams;
            [JsonProperty("Могут тиммейты удалять постройки?")]
            public bool canTeammatesDeleteBuildings;
        }
        
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData()
            {
                useTeams = true,
                canTeammatesDeleteBuildings = true
            };
            SaveConfig(config);
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            cfg = Config.ReadObject<ConfigData>();
            SaveConfig(cfg);
        }

        private void SaveConfig(object config)
        {
            Config.WriteObject(config, true);
        }
        #endregion
    }
}