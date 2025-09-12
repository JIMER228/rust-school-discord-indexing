using System;
using System.Collections.Generic;
using System.Linq;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;
using WebSocketSharp;

namespace Oxide.Plugins
{
    [Info("BTree", "Hougan", "0.0.1")]
    public class BTree : RustPlugin
    {
        #region Classes

        private class StoredTree
        {
            [JsonProperty("Позиция")]
            public string Position;
            [JsonProperty("Стадия")]
            public int Stage;

            public static StoredTree FromTree(MagicTree tree)
            {
                return new StoredTree
                {
                    Position = tree.transform.position.ToString(),
                    Stage    = tree.StageIndex
                };
            }

            public void ToTree()
            {
                var ent = GameManager.server.CreateEntity("assets/bundled/prefabs/radtown/foodbox.prefab", Position.ToVector3());
                ent.gameObject.AddComponent<MagicTree>().InitializeTree(Stage); 
            }
        }
        
        private class BoxItem
        {
            [JsonProperty("Короткое название предмета")]
            public string ShortName;
            [JsonProperty("SkinID")]
            public ulong SkinID;

            [JsonProperty("Вес предмета в рандоме")] 
            public int Weight;

            [JsonProperty("Мин. количество")]
            public int MinAmount;
            [JsonProperty("Макс. количество")]
            public int MaxAmount;

            public Item ToItem() => ItemManager.CreateByName(ShortName, Oxide.Core.Random.Range(MinAmount, MaxAmount), SkinID);
        }
        
        private class BoxKit
        {
            [JsonProperty("Минимальное число предметов")]
            public int MinAmount = 1;
            [JsonProperty("Максимальное число предметов")]
            public int MaxAmount = 5;

            [JsonProperty("Возможные для выпадения предметы")]
            public List<BoxItem> Items = new List<BoxItem>();

            public BoxItem GetControlRandom()
            {
                int maxChance = Items.Sum(p => p.Weight);
                int rChance = Oxide.Core.Random.Range(0, maxChance);

                int curChance = 0;
                foreach (var check in Items.OrderBy(p => p.Weight))
                {
                    if (check.Weight + curChance > rChance)
                        return check;

                    curChance += check.Weight;
                }

                Interface.Oxide.LogError($"Unable to find random weight item ([{maxChance}/{rChance}/{curChance}])"); 
                return Items.GetRandom();
            }
            
            public void ProcessItems(StorageContainer container)
            {
                for (int i = 0; i < Oxide.Core.Random.Range(MinAmount, MaxAmount); i++)
                {
                    var random = GetControlRandom();
                    random.ToItem().MoveToContainer(container.inventory);
                }
            }
        }

        private static class StageInstance
        {
            public static Dictionary<TerrainBiome.Enum, int> Radius = new Dictionary<TerrainBiome.Enum, int>
            {
                [TerrainBiome.Enum.Temperate] = 5,
                [TerrainBiome.Enum.Arid] = 2,
                [TerrainBiome.Enum.Arctic] = 1,
                [TerrainBiome.Enum.Tundra] = 3,
            };
            public static Dictionary<TerrainBiome.Enum, int> Heights = new Dictionary<TerrainBiome.Enum, int>
            {
                [TerrainBiome.Enum.Temperate] = 6,
                [TerrainBiome.Enum.Arid] = 17,
                [TerrainBiome.Enum.Arctic] = 23,
                [TerrainBiome.Enum.Tundra] = 8,
            };
            [JsonProperty("Префабы деревьев для разных стадий")]
            public static Dictionary<TerrainBiome.Enum, List<string>> Trees = new Dictionary<TerrainBiome.Enum, List<string>>
            {
                [TerrainBiome.Enum.Temperate] = new List<string>
                {
                    "assets/prefabs/plants/corn/corn.entity.prefab", 
                    "assets/bundled/prefabs/autospawn/resource/v2_temp_forest/birch_tiny_temp.prefab", 
                    "assets/bundled/prefabs/autospawn/resource/v2_temp_forest/birch_small_temp.prefab",
                    "assets/bundled/prefabs/autospawn/resource/v2_temp_forest/birch_medium_temp.prefab", 
                    "assets/bundled/prefabs/autospawn/resource/v2_temp_forest/birch_large_temp.prefab",
                    "assets/bundled/prefabs/autospawn/resource/v2_temp_field_large/oak_b.prefab"
                }, 
                [TerrainBiome.Enum.Arid] = new List<string>
                {
                    "assets/prefabs/plants/pumpkin/pumpkin.entity.prefab", 
                    "assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_short_a_entity.prefab", 
                    "assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_small_c_entity.prefab",
                    "assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_small_b_entity.prefab", 
                    "assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_med_a_entity.prefab",
                    "assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_tall_a_entity.prefab"
                }, 
                [TerrainBiome.Enum.Arctic] = new List<string>
                {
                    "assets/prefabs/plants/corn/corn.entity.prefab", 
                    "assets/bundled/prefabs/autospawn/resource/v2_temp_forest_deciduous_small/american_beech_e_dead.prefab", 
                    "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest_snow/pine_d_snow.prefab",
                    "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest/douglas_fir_a_snow.prefab", 
                    "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest_snow/pine_a_snow.prefab",
                    "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest_snow/pine_c_snow.prefab"
                }, 
                [TerrainBiome.Enum.Tundra] = new List<string>
                {
                    "assets/prefabs/plants/pumpkin/pumpkin.entity.prefab", 
                    "assets/bundled/prefabs/autospawn/resource/v2_tundra_forest/birch_tiny_tundra.prefab", 
                    "assets/bundled/prefabs/autospawn/resource/v2_temp_forest_deciduous_small/american_beech_e_dead.prefab",
                    "assets/bundled/prefabs/autospawn/resource/v2_tundra_forest_small/oak_f_tundra.prefab", 
                    "assets/bundled/prefabs/autospawn/resource/v2_tundra_forest/oak_a_tundra.prefab",
                    "assets/bundled/prefabs/autospawn/resource/v2_tundra_forest/oak_b_tundra.prefab"
                }
            };
        }

        private class Configuration
        {
            [JsonProperty("Шанс выпадения семечка")]
            public int DropChance = 20;
            [JsonProperty("Интервал между стадиями роста")]
            public int StageInterval = 3;
            [JsonProperty("Количество ящиков на дереве")]
            public int BoxAmount = 3;
            [JsonProperty("SkinID предмета")]
            public ulong SkinID = 1244;
            
            [JsonProperty("Настройки выпадающих предметов")]
            public BoxKit Items = new BoxKit();

            public static Configuration Generate()
            {
                return new Configuration
                {
                    Items = new BoxKit
                    {
                        MinAmount = 3, 
                        MaxAmount = 10,
                        Items = new List<BoxItem>
                        {
                            new BoxItem
                            {
                                ShortName = "rifle.ak",
                                MinAmount = 1,
                                MaxAmount = 3,
                                SkinID = 123
                            }
                        }
                    }
                };
            }
        }
        
        private class MagicTree : MonoBehaviour
        {
            public List<BaseEntity> Boxes = new List<BaseEntity>();
            public BaseEntity Entity;
            
            public int StageIndex;
            public int TimeLeftFromStage;
            public int MaxStage = StageInstance.Trees.Count - 1;

            public bool ShouldSave = true;
            
            public void Awake() {
                Entity = GetComponent<BaseEntity>();
                MaxStage = StageInstance.Trees[Biome()].Count - 1;
            }

            public TerrainBiome.Enum Biome()
            {
                return (TerrainBiome.Enum) TerrainMeta.BiomeMap.GetBiomeMaxType(transform.position);
            }
            public void InitializeTree(int stageIndex)
            {
                InvokeRepeating(nameof(UpdateTime), 1f, 1f); 
                StageIndex = stageIndex;

                var prefab = StageInstance.Trees[Biome()].ElementAtOrDefault(stageIndex);
                if (prefab == null)
                {
                    Interface.Oxide.LogError($"Unknown stage of tree: {transform.position} [{stageIndex}]");
                    SelfDestroy();
                    return;
                }

                if (Entity.PrefabName != prefab)
                { 
                    Effect.server.Run("assets/bundled/prefabs/fx/ore_break.prefab", this.transform.position + new Vector3(0, -2, 0));
                    var newTree = GameManager.server.CreateEntity(prefab, transform.position);
                    newTree.Spawn();

                    newTree.gameObject.AddComponent<MagicTree>().InitializeTree(stageIndex);
                    SelfDestroy();
                    return;
                }

                var nextStage = StageInstance.Trees[Biome()].ElementAtOrDefault(stageIndex + 1);
                if (nextStage != null)
                {
                    Invoke(nameof(SwitchFaze), Settings.StageInterval);
                    return;
                }
                
                SpawnBoxes();
            }

            public void UpdateTime()
            {
                if (!ShouldSave) return; 
                
                if (IsLast(StageIndex, StageInstance.Trees[Biome()].Count - 1))
                    TimeLeftFromStage = Settings.StageInterval;
                
                if (TimeLeftFromStage < Settings.StageInterval)
                    TimeLeftFromStage++;

                string text = "";
                if (TimeLeftFromStage == Settings.StageInterval && IsLast(StageIndex, StageInstance.Trees[Biome()].Count - 1))
                {
                    text = $"<size=18>Магическое Древо</size>\n"                                                      +
                            $"<size=13>МОЖНО РУБИТЬ</size>\n"                                            +
							$"<color=#d3c9c0><size=17>●</size></color>";
                }
                else if (TimeLeftFromStage != Settings.StageInterval)
                {
                    text = 
                            $"<size=20><b>{((float) TimeLeftFromStage / Settings.StageInterval * 100).ToString("F0")}% ({Settings.StageInterval - TimeLeftFromStage} сек.)\n</b></size>" +
                            $"Стадия: <b>{StageIndex + 1}</b>/<b>{MaxStage + 1}</b>\n"                                    +
                            $"<color=#d3c9c0><size=17>●</size></color>";
                }
                else
                {
                    text = $"<size=52>⇡</size>" +
                            $"";
                }
                
                var ent = new List<BasePlayer>();
                Vis.Entities(transform.position, 2, ent);

                foreach (var check in ent.Where(p => p.IsConnected))
                {
                    check.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);

                    Vector3 position = transform.position;
                    if (StageIndex != 0)
                        position += new Vector3(0, 1, 0);
                    
                    check.SendEntityUpdate(); 
                    check.SendConsoleCommand("ddraw.text", 1f, Color.white, position, text);
                    check.SendConsoleCommand("camspeed 0");
                    
                    if (check.Connection.authLevel < 2)
                        check.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                    
                    check.SendEntityUpdate();
                }
            }

            public void SpawnBoxes()
            {
                for (int i = 0; i < Settings.BoxAmount; i++)
                {
                    var box = GameManager.server.CreateEntity("assets/bundled/prefabs/radtown/crate_underwater_basic.prefab", transform.position, new Quaternion());
                    box.transform.position = box.transform.position + new Vector3(0.0f, StageInstance.Heights[Biome()], 0.0f) + UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(-StageInstance.Radius[Biome()], StageInstance.Radius[Biome()]);
                    box.Spawn();

                    var container = box.GetComponent<LootContainer>();
                    InvokeHandler.CancelInvoke(container, container.SpawnLoot); 
                    
                    container.inventory.Clear();
                    container.inventory.itemList.Clear();
                    container.inventorySlots           = 36;
                    container.inventory.capacity       = 36;
                    container.minSecondsBetweenRefresh = 0f;
                    container.maxSecondsBetweenRefresh = 0f;
                    
                    Settings.Items.ProcessItems(container); 
                    Boxes.Add(box); 
                }
            }

            public void Untie()
            {
                foreach (var check in Boxes.Where(p => p != null && !p.IsDestroyed))
                {
                    var body = check.GetComponent<Rigidbody>();
                
                    if (body != null)
                    { 
                        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        body.isKinematic = false;

                        check.gameObject.AddComponent<AntiUntie>();
                    } 
                }
            }

            public void SwitchFaze() => InitializeTree(StageIndex + 1);
            public void SelfDestroy()
            {
                if (!Entity.IsDestroyed)
                    Entity?.KillMessage();
            }

            public void OnDestroy()
            {
                Untie();

                if (!Entity.IsDestroyed)
                {
                    Entity.SendNetworkUpdate();
                    Entity?.Kill();
                }
            }    
        }

        public class AntiUntie : MonoBehaviour
        {
            BaseEntity check;
             
            public void Awake()
            {
                check = GetComponent<BaseEntity>();
                InvokeRepeating(nameof(Untie), 0f, 0.5f); 
            }

            public void Untie()
            {
                check.SetFlag(BaseEntity.Flags.Reserved8, false, false);
                var body = check.GetComponent<Rigidbody>();
                
                if (body != null)
                { 
                    body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    body.isKinematic            = false;
                } 
            }
        }
        
        #endregion

        #region Variables

        private static BTree _;
        private static Configuration Settings;

        #endregion

        #region CommandsЭ

        [ChatCommand("sa")]
        private void Cmdsad(BasePlayer player)
        {
            Server.Broadcast(((TerrainBiome.Enum)(TerrainMeta.BiomeMap.GetBiomeMaxType(player.transform.position))).ToString());
        }

        [ConsoleCommand("givemagictree")]
        private void cmdGive(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player() ?? null;
            if (player?.net.connection.authLevel < 2) return;
            
            if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, "bad syntax");
                return;
            }

            BasePlayer targetPlayer = BasePlayer.Find(arg.Args[0]);
            if (targetPlayer == null)
            {
                SendReply(arg, "error player not found for give");
                return;
            }

            GiveTree(targetPlayer);
        }

        private void GiveTree(BasePlayer player)
        {
            Item item = ItemManager.CreateByName("seed.corn", 1, Settings.SkinID);
            if (!player.inventory.GiveItem(item))
            {
                item.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity, new Quaternion());
            }
        }

        #endregion
        
        #region Hooks

        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (!player.IsAdmin || info == null) return;

            var obj = info.HitEntity;
            if (obj == null) return;

            if (!obj.PrefabName.Contains("foodbox")) return;
            var comp = obj.gameObject.AddComponent<MagicTree>();
            comp.InitializeTree(StageInstance.Trees[comp.Biome()].Count - 1); 
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Settings = Config.ReadObject<Configuration>();
                if (Settings == null) LoadDefaultConfig();
            } 
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }
            
            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => Settings = Configuration.Generate();
        protected override void SaveConfig()        => Config.WriteObject(Settings);
        
        private static int constructions = LayerMask.GetMask("Construction", "Deployable", "Prevent Building", "Deployed");
        public bool IsOutside(Vector3 position)
        { 
            return !UnityEngine.Physics.Raycast(position, Vector3.up, 100f, 1101070337) && !UnityEngine.Physics.Raycast(position, Vector3.down, 3f, constructions);
        }
        
        public bool IsOutside(BaseEntity entity)
        {
            OBB obb = entity.WorldSpaceBounds();
            return IsOutside(obb.position + obb.up * obb.extents.y);
        }
        
        private void OnEntityBuilt(Planner plan, GameObject obj)
        {
            var entity = obj.GetComponent<BaseEntity>();
            if (entity != null && entity.ShortPrefabName == "corn.entity" && entity.skinID == Settings.SkinID)
            {
                if (!IsOutside(entity))
                {
                    var player = plan.GetOwnerPlayer();
                    GiveTree(player);
                    NextTick(() => {
                    entity?.Kill();
                    });
                    return;
                }

                entity.enableSaving = false;
                plan.GetOwnerPlayer().SendConsoleCommand($"note.inv 605467368 1 \"<color=#d4d8ca>+{0.01f}</color><color=#b9bdaf>EXP</color>\"");
                plugins.Find("Quest")?.Call("AddEXP", plan.GetOwnerPlayer(), 0.01f, "За посадку дерева"); 
                var newObj = entity.gameObject.AddComponent<MagicTree>();
                NextTick(() =>
                {
                    newObj.InitializeTree(0); 
                });
            }
        }
        
        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null) return; 
            
            if (dispenser.gatherType == ResourceDispenser.GatherType.Tree && dispenser.GetComponent<MagicTree>() == null)
            {
                if (Oxide.Core.Random.Range(0, 100) < Settings.DropChance)
                {
                    global::ItemManager.CreateByName("seed.corn", 1, 2226179304).MoveToContainer(player.inventory.containerMain); 
                }
            }
            
            if (dispenser == null || dispenser.gatherType != ResourceDispenser.GatherType.Tree) return;
            var obj = dispenser.GetComponent<MagicTree>();
            
            if (obj == null || !IsLast(obj.StageIndex, StageInstance.Trees[obj.Biome()].Count - 1)) return;
            obj.Untie();   
            return;
        }

        private void OnServerInitialized()
        {
            _ = this;

            if (DateTime.Now.Subtract(SaveRestore.SaveCreatedTime).TotalMinutes < 300)
            {   
                UnityEngine.Object.FindObjectsOfType<MagicTree>().ToList().ForEach(UnityEngine.Object.Destroy);
                PrintError($"Wipe detected [{DateTime.Now.Subtract(SaveRestore.SaveCreatedTime).TotalMinutes:F0}]! Removing trees..."); 
            }
            else 
            { 
                PrintError($"Wipe not detected! Was: {DateTime.Now.Subtract(SaveRestore.SaveCreatedTime).TotalMinutes:F0} min ago."); 
				Server.Command("del assets/bundled/prefabs/radtown/crate_underwater_basic.prefab");
                /*if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name))
                {
                    var list = Interface.Oxide.DataFileSystem.ReadObject<List<StoredTree>>(Name);

                    foreach (var check in list)
                        check.ToTree();
                }*/
				
            }
			
        }

        private void Unload()
        {
            //SaveData();
            UnityEngine.Object.FindObjectsOfType<MagicTree>().ToList().ForEach(UnityEngine.Object.Destroy);
        } 
        
        #endregion

        #region Methods

        private static bool IsLast(int stage, int max) => stage == max;

        #endregion
    }
}