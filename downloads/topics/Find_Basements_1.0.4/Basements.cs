

using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using Facepunch.Extend;
using Newtonsoft.Json.Converters;
using UnityEngine;
using Oxide.Core.Libraries.Covalence;
using Oxide.Plugins.BasementMethods;
using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Plugins;
using System;

namespace Oxide.Plugins
{
    [Info("Basements", "mr01sam", "1.0.4")]
    [Description("Allows players to build basements underneath their base.")]
    public partial class Basements : CovalencePlugin
    {
        public static Basements INSTANCE;

        public const string PermissionBuild = "basements.build";

        public const string PermissionAdmin = "basements.admin";

        public const string PermissionFree = "basements.free";

        public bool allowDestroy = false;
        public bool newSaveLoaded = false;

        public static bool debugging = false;
        public static void DEBUG(string message)
        {
            if (debugging)
            {
                INSTANCE?.Puts(message);
            }
        }

        void Init()
        {
            INSTANCE = this;
            UnsubscribeAll(
                nameof(OnLootEntity),
                nameof(OnLootEntityEnd),
                nameof(OnActiveItemChanged),
                nameof(OnStructureRepair),
                nameof(OnEntitySpawned),
                nameof(OnEntityKill),
                nameof(OnEntityDeath),
                nameof(OnStructureDemolish),
                nameof(OnStructureRotate),
                nameof(OnStructureUpgrade),
                nameof(OnEntityTakeDamage),
                nameof(CanDemolish),
                nameof(OnDoorOpened),
                nameof(CanBuild),
                nameof(OnBuildingSplit),
                nameof(OnBuildingMerge),
                nameof(OnPlayerViolation),
                nameof(OnHammerHit),
                nameof(CanLock),
                nameof(CanUnlock),
                nameof(CanChangeCode)
            );
        }

        void OnServerInitialized()
        {
            // Prevent auto suicide if underground
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "antihack.terrain_protection 0");

            if (!permission.PermissionExists(PermissionFree)) { permission.RegisterPermission(PermissionFree, this); }

            if (!newSaveLoaded)
            {
                LoadData();
            }

            SubscribeAll();
            if (!config.SyncLocksOnBasementEntrances)
            {
                Unsubscribe(nameof(CanLock));
                Unsubscribe(nameof(CanUnlock));
                Unsubscribe(nameof(CanChangeCode));
            }
        }

        void Unload()
        {
            SaveData();

            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                if (basePlayer == null) { continue; }
                DestroyToolcupboardButton(basePlayer);
                DestroyPlacementBanner(basePlayer);
                DestroyInsufficientResources(basePlayer);
            }
        }

        void OnNewSave(string strFilename)
        {
            Puts("Map wipe detected, current data files will be cleared upon save.");
            newSaveLoaded = true;
        }

        void OnServerSave()
        {
            SaveData();
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"Basements/BasementEntities", BasementEntities);
            Interface.Oxide.DataFileSystem.WriteObject($"Basements/BasementEntrances", BasementEntrances);
            Interface.Oxide.DataFileSystem.WriteObject($"Basements/BasementStructures", BasementStructures);
        }

        void LoadData()
        {
            BasementEntities = Interface.Oxide.DataFileSystem.ReadObject<HashSet<ulong>>($"Basements/BasementEntities") ?? new HashSet<ulong>();
            BasementEntrances = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, BasementEntrance>>($"Basements/BasementEntrances") ?? new Dictionary<ulong, BasementEntrance>();
            BasementStructures = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<uint, BasementStructure>>($"Basements/BasementStructures") ?? new Dictionary<uint, BasementStructure>();
            Puts($"Loaded {BasementStructures.Count} structures {BasementEntrances.Count} entrances {BasementEntities.Count} entities");
        }

        private List<string> _subscriptions = new List<string>();

        public void UnsubscribeAll(params string[] methods)
        {
            _subscriptions.AddRange(methods);
            foreach (var method in _subscriptions) { Unsubscribe(method); }
        }

        public void SubscribeAll()
        {
            foreach (var method in _subscriptions)
            {
                switch (method)
                {
                    default:
                        Subscribe(method);
                        break;
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    public partial class Basements
    {
        [HookMethod("IsBasementEntity")]
        private bool API_IsBasementEntity(ulong entityId)
        {
            return BasementEntities.Contains(entityId);
        }

        [HookMethod("GetBasementBuildingIds")]
        private uint[] API_GetBasementBuildingIds(uint surfaceBuildingId)
        {
            var surfaceBuilding = BuildingManager.server.GetBuilding(surfaceBuildingId);
            if (surfaceBuilding == null) { return Array.Empty<uint>(); }
            var list = GetBasementsConnectedToSurfaceBuilding(surfaceBuilding);
            var result = new uint[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                result[i] = list[i].BasementBuildingId;
            }
            return result;
        }

        [HookMethod("GetSurfaceBuildingIds")]
        private uint[] API_GetSurfaceBuildingIds(uint basementBuildingId)
        {
            var basement = BasementStructures.GetValueOrDefault(basementBuildingId);
            if (basement == null) { return Array.Empty<uint>(); }
            return basement.SurfaceBuildingIds.ToArray();
        }
    }
}

namespace Oxide.Plugins
{
    public partial class Basements
    {
        // Key is basement building id
        public Dictionary<uint, BasementStructure> BasementStructures = new Dictionary<uint, BasementStructure>();

        // Key is hatch id
        public Dictionary<ulong, BasementEntrance> BasementEntrances = new Dictionary<ulong, BasementEntrance>();

        // Key is entity id
        public HashSet<ulong> BasementEntities = new HashSet<ulong>();


        #region Models
        public class BasementEntrance
        {
            public ulong HatchId;
            public bool IsDownHatch;
            public ulong ConnectedHatchId;


            [JsonIgnore]
            private Door _baseEntity = null;

            [JsonIgnore]
            public Door BaseEntity
            {
                get
                {
                    if (_baseEntity == null)
                    {
                        _baseEntity = FindBaseEntity(HatchId) as Door;
                    }
                    return _baseEntity;
                }
            }

            [JsonIgnore]
            private BasementEntrance _connectedHatch = null;

            [JsonIgnore]
            public BasementEntrance ConnectedHatch
            {
                get
                {
                    if (_connectedHatch == null)
                    {
                        var door = FindBaseEntity(ConnectedHatchId) as Door;
                        _connectedHatch = door == null ? null : INSTANCE.BasementEntrances.GetValueOrDefault(door.net.ID.Value);
                    }
                    return _connectedHatch;
                }
            }

            [JsonIgnore]
            private BuildingBlock _parentBuildingBlock = null;

            [JsonIgnore]
            public BuildingBlock ParentBuildingBlock
            {
                get
                {
                    if (_parentBuildingBlock == null)
                    {
                        _parentBuildingBlock = BaseEntity?.GetParentEntity() as BuildingBlock;
                    }
                    return _parentBuildingBlock;
                }
            }

            [JsonIgnore]
            public uint BasementBuildingId => (IsDownHatch ? ConnectedHatch?.ParentBuildingBlock?.buildingID : ParentBuildingBlock?.buildingID) ?? 0;

            [JsonIgnore]
            public BuildingManager.Building BasementBuilding => BuildingManager.server.GetBuilding(BasementBuildingId);

            [JsonIgnore]
            public BasementStructure Basement => INSTANCE.BasementStructures.GetValueOrDefault(BasementBuildingId);

            [JsonIgnore]
            public uint SurfaceBuildingId => (IsDownHatch ? ParentBuildingBlock?.buildingID : ConnectedHatch?.ParentBuildingBlock?.buildingID) ?? 0;

            [JsonIgnore]
            public BuildingManager.Building SurfaceBuilding => BuildingManager.server.GetBuilding(SurfaceBuildingId);

            public void ClearCache()
            {
                _baseEntity = null;
                _connectedHatch = null;
                _parentBuildingBlock = null;
            }
        }

        public class BasementStructure
        {
            public uint BasementBuildingId;
            public float Elevation;
            public bool ShownTip;

            [JsonIgnore]
            public BuildingManager.Building BasementBuilding => BuildingManager.server.GetBuilding(BasementBuildingId);

            [JsonIgnore]
            public IEnumerable<BuildingBlock> BasementBlocks => BasementBuilding?.buildingBlocks.Where(x => x.IsBasementEntity()) ?? Array.Empty<BuildingBlock>();


            [JsonIgnore]
            private List<BasementEntrance> _entrances = null;

            [JsonIgnore]
            public int EntrancePairCount = 0;

            [JsonIgnore]
            public IEnumerable<BasementEntrance> Entrances
            {
                get
                {
                    if (_entrances == null)
                    {
                        EntrancePairCount = 0;
                        _entrances = new List<BasementEntrance>();
                        if (BasementBuilding != null)
                        {
                            foreach (var decay in BasementBuilding.decayEntities)
                            {
                                if (decay != null && decay.IsBasementEntrance())
                                {
                                    var entrance = INSTANCE.BasementEntrances[decay.net.ID.Value];
                                    _entrances.Add(entrance);
                                    EntrancePairCount++;
                                    var connectedEntrance = entrance.ConnectedHatch;
                                    if (connectedEntrance != null)
                                    {
                                        _entrances.Add(connectedEntrance);
                                    }
                                }
                            }
                        }
                    }
                    return _entrances;
                }
            }

            [JsonIgnore]
            public IEnumerable<uint> SurfaceBuildingIds
            {
                get
                {
                    HashSet<uint> ids = new HashSet<uint>();
                    foreach (var entrance in Entrances)
                    {
                        if (entrance.IsDownHatch)
                        {
                            ids.Add(entrance.SurfaceBuildingId);
                        }
                    }
                    return ids;
                }
            }

            [JsonIgnore]
            public BuildingManager.Building FirstSurfaceBuilding => SurfaceBuildingIds.Any() == false ? null :BuildingManager.server.GetBuilding(SurfaceBuildingIds.First());

            public void ClearCache()
            {
                _entrances = null;
            }
        }
        #endregion

        #region Helper Methods
        public float GetProjectedBasementElevation(BuildingBlock start)
        {
            var foundations = GetFoundationsForBasement(start, BuildingGrade.Enum.Stone);

            // Find lowest foundation
            BuildingBlock lowestFoundation = null;
            foreach (var foundation in foundations)
            {
                if (lowestFoundation == null || lowestFoundation.transform.position.y > foundation.transform.position.y)
                {
                    lowestFoundation = foundation;
                }
            }

            var diff = (int)Math.Floor(start.transform.position.y - lowestFoundation.transform.position.y);
            var iseven = diff % 3 == 0;
            var elevation = lowestFoundation.transform.position.y + -6f;
            if (!iseven)
            {
                elevation -= 1.5f;
            }
            elevation -= 3f;

            // Uncomment if I ever want to disallow water basements
            //if (WaterLevel.Test(new Vector3(start.transform.position.x, elevation, start.transform.position.z), false, false))
            //{
            //    // If its in water, make it under the surface
            //    elevation = TerrainMeta.HeightMap.GetHeight(start.transform.position) - 6f;
            //}

            return elevation;
        }


        private List<BasementEntrance> _returnGetEntrancesForSurfaceBuilding = new List<BasementEntrance>();
        public List<BasementEntrance> GetDownEntrancesForSurfaceBuilding(BuildingManager.Building surfaceBuilding)
        {
            _returnGetEntrancesForSurfaceBuilding.Clear();
            if (surfaceBuilding == null || surfaceBuilding.buildingBlocks == null) { return _returnGetEntrancesForSurfaceBuilding; }
            foreach (var block in surfaceBuilding.buildingBlocks)
            {
                if (block == null) { continue; }
                if (block.IsFoundation() && block.children != null)
                {
                    Door childHatch = null;
                    foreach (var child in block.children)
                    {
                        if (child is Door door && door.IsBasementEntrance())
                        {
                            childHatch = door;
                            break;
                        }
                    }
                    if (childHatch != null && childHatch.net != null)
                    {
                        var entrance = INSTANCE.BasementEntrances[childHatch.net.ID.Value];
                        if (entrance != null)
                        {
                            _returnGetEntrancesForSurfaceBuilding.Add(entrance);
                        }
                    }
                }
            }
            return _returnGetEntrancesForSurfaceBuilding;
        }

        private List<BasementStructure> _returnGetBasementsConnectedToSurfaceBuilding = new List<BasementStructure>();
        public List<BasementStructure> GetBasementsConnectedToSurfaceBuilding(BuildingManager.Building surfaceBuilding)
        {
            _returnGetBasementsConnectedToSurfaceBuilding.Clear();
            HashSet<uint> seen = new HashSet<uint>();

            foreach (var entrance in GetDownEntrancesForSurfaceBuilding(surfaceBuilding))
            {
                var id = entrance.BasementBuildingId;
                if (id == 0 || !seen.Add(id)) continue;

                var basement = BasementStructures.GetValueOrDefault(id);
                if (basement != null)
                {
                    _returnGetBasementsConnectedToSurfaceBuilding.Add(basement);
                }
            }
            return _returnGetBasementsConnectedToSurfaceBuilding;
        }
        #endregion


        #region Main Methods
        public BasementStructure SpawnBasement(BuildingBlock surfaceFoundation)
        {
            if (surfaceFoundation == null) { return null; }
            var building = surfaceFoundation.GetBuilding();
            var basements = GetBasementsConnectedToSurfaceBuilding(building);


            BasementStructure basement = null;
            if (basements.Count != 0)
            {
                // Try to get existing basement that exists directly under this foundation
                basement = basements.Select(x => x.BasementBuilding?.GetBasementFoundationFromSurface(surfaceFoundation)).FirstOrDefault(x => x != null)?.GetBasement();
            }
            if (basement == null)
            {
                // Spawn new basement because there isn't one under this foundation
                basement = new BasementStructure
                {
                    // If there are other basements, we want the elevations to match
                    Elevation = basements.Count == 0 ? GetProjectedBasementElevation(surfaceFoundation) : basements.First().Elevation,
                    BasementBuildingId = BuildingManager.server.NewBuildingID()
                };

                // Spawn basement cell at elevation
                var basementFoundation = Spawn1x1BasementCell(basement, surfaceFoundation);
                var effect = new Effect(Effects.PromoteStone, surfaceFoundation, 0, Vector3.zero, Vector3.zero);
                effect.Play();
                timer.Repeat(0.5f, 2, () =>
                {
                    effect.Play();
                });
                BasementStructures[basement.BasementBuildingId] = basement;
            }

            // Spawn ladder hatches
            timer.In(1f, () => // Allow animations to finish
            {
                if (surfaceFoundation == null) { return; }
                SpawnLadderHatchPair(basement, surfaceFoundation, out _, out _);
                basement?.ClearCache();
            });
            return basement;
        }
        #endregion

        public void SpawnMissingExteriorWalls(BasementStructure basement)
        {
            if (basement == null) { return; }
            foreach (var block in basement.BasementBlocks)
            {
                if (block.IsNetNull() || !block.IsFoundation() || !block.IsBasementEntity()) { continue; }
                var triangle = block.IsTriangle();
                for (int d = 1; d <= (triangle ? 3 : 4); d++)
                {
                    // check if theres a foundation in that direction
                    var foundation = block.GetLinkedFoundation(d);
                    if (foundation == null)
                    {
                        // spawn a wall if theres no foundation and no wall
                        var wall = block.GetLinkedWall(d, out Vector3 pos, out Vector3 rot);
                        if (wall == null)
                        {
                            SpawnBasementBlock(basement, Prefabs.Wall, block,
                                position: block.transform.position,
                                rotation: block.transform.rotation,
                                localPosition: pos,
                                localRotation: rot,
                                ownerid: block.OwnerID
                            );
                        }
                    }
                }
                // check if theres a ceiling
                var ceiling = block.GetCeilingAboveFoundation();
                if (ceiling == null)
                {
                    SpawnBasementBlock(basement, triangle ? Prefabs.TriangleFloor : Prefabs.Floor, block,
                        position: block.transform.position,
                        rotation: block.transform.rotation,
                        localPosition: new Vector3(0, 3f, 0),
                        ownerid: block.OwnerID
                    );
                }
            }
        }

        public void DestroyBasementCell(BuildingBlock basementFoundation)
        {
            if (basementFoundation != null)
            {
                TeleportBasementContents(basementFoundation);
                var ceiling = basementFoundation.GetCeilingAboveFoundation();
                KillBasementEntity(ceiling);
                for (int d = 1; d <= 4; d++)
                {
                    var wall = basementFoundation.GetLinkedWall(d, out Vector3 pos, out Vector3 rot);
                    KillBasementEntity(wall);
                }
                KillBasementEntity(basementFoundation);
            }
        }

        private BuildingBlock GetSurfaceFoundation(BuildingBlock basementFoundation, BuildingManager.Building surfaceBuilding)
        {
            if (basementFoundation == null) { return null; }
            foreach (var block in surfaceBuilding?.buildingBlocks)
            {
                if (block == null || !block.ShortPrefabName.Contains("foundation") || block.IsBasementEntity())
                {
                    continue;
                }
                if (EqualsPosition2D(basementFoundation.WorldSpaceBounds().ToBounds().center, block.WorldSpaceBounds().ToBounds().center))
                {
                    return block;
                }
            }
            return null;
        }

        private BuildingBlock GetBasementFoundation(BuildingBlock surfaceFoundation, BuildingManager.Building basementBuilding)
        {
            if (surfaceFoundation == null) { return null; }
            foreach (var block in basementBuilding?.buildingBlocks)
            {
                if (block == null || !block.ShortPrefabName.Contains("foundation") || block.IsBasementEntity())
                {
                    continue;
                }
                if (EqualsPosition2D(block.transform.position, surfaceFoundation.transform.position))
                {
                    return block;
                }
            }
            return null;
        }

        private BuildingBlock GetBasementFoundation(BuildingBlock surfaceFoundation)
        {
            if (surfaceFoundation == null) { return null; }
            return RaycastVertical(surfaceFoundation, Vector3.down, 100f, (x) => (x is BuildingBlock casted && casted.IsBasementEntity() && casted.IsFoundation())) as BuildingBlock;
        }

        private void DestroyBlocksAbove(BaseEntity entity, float distance)
        {
            foreach(var hit in GetEntitiesAbove(entity, distance))
            {
                if (!(hit is StabilityEntity block)) { continue; }
                
                hit.Kill(BaseNetworkable.DestroyMode.Gib);
            }
        }

        List<BaseEntity> hitList = new List<BaseEntity>();
        List<BaseEntity> GetEntitiesAbove(BaseEntity block, float height)
        {
            Vector3 startPos = block.CenterPoint().Mod(y: 0.4f);
            hitList.Clear();
            float radius = 0.3f;
            foreach (var hit in Physics.SphereCastAll(startPos, radius, Vector3.up, height))
            {
                hitList.Add(hit.GetEntity());
            }

            return hitList;
        }

        public void KillBasementEntity(BaseEntity entity, BasementStructure basement = null, bool gibs = false)
        {
            if (entity == null || entity.IsDestroyed) { return; }
            var eid = entity.net.ID.Value;
            BasementEntities.Remove(eid);
            destroying = true;
            entity.Kill(gibs ? BaseNetworkable.DestroyMode.Gib : BaseNetworkable.DestroyMode.None);
            if (entity is DecayEntity de)
            {
                BuildingManager.server.Remove(de);
            }
            destroying = false;
        }

        public void DestroyBasementEntrancePair(BasementEntrance entrance, bool gibs = false)
        {
            if (entrance == null) { return; }
            var connected = entrance.ConnectedHatch;
            KillBasementEntity(entrance.BaseEntity, gibs: gibs);
            KillBasementEntity(connected?.BaseEntity, gibs: gibs);
            BasementEntrances.Remove(entrance.HatchId);
            if (connected != null)
            {
                BasementEntrances.Remove(connected.HatchId);
            }
        }

        public void DestroyBasement(BasementStructure basement)
        {
            if (basement == null) { return; }
            var buildingId = basement.BasementBuildingId;
            var blocks = basement.BasementBlocks?.ToArray() ?? Array.Empty<BuildingBlock>();
            basement?.ClearCache();
            foreach (var entrance in basement.Entrances.ToArray())
            {
                DestroyBasementEntrancePair(entrance, gibs: false);
            }
            foreach (var block in blocks)
            {
                if (block.IsNetNull() || !block.IsBasementEntity() || !block.IsFoundation()) { continue; }
                // Move all entities
                TeleportBasementContents(block);
            }
            foreach (var entity in blocks)
            {
                if (entity.IsNetNull() || !entity.IsBasementEntity()) { continue; }
                // Kill
                try
                {
                    foreach (var child in entity.children)
                    {
                        if (child == null) { continue; }
                        KillBasementEntity(child, basement);
                    }
                    KillBasementEntity(entity, basement);
                }
                catch (Exception)
                {
                    // Failed to kill exterior entity
                }
            }
            BasementStructures.Remove(buildingId);
        }

        public int GetDirOfWall(BuildingBlock foundation, BuildingBlock targetWall)
        {
            if (foundation == null || targetWall == null) { return 0; }
            for(int i = 1; i <= 4; i++)
            {
                var wall = foundation.GetLinkedWall(i, out _, out _);
                if (wall == null) { continue; }
                if (wall.EqualNetID(targetWall))
                {
                    return i;
                }
            }
            return 0;
        }

        private readonly List<BaseCombatEntity> _nearbyCombatEntities = new List<BaseCombatEntity>();
        public void TeleportBasementContents(BuildingBlock basementFoundation)
        {
            if (basementFoundation.IsFoundation())
            {
                _nearbyCombatEntities.Clear();
                Vis.Entities(basementFoundation.transform.position, 2f, _nearbyCombatEntities);
                foreach (var target in _nearbyCombatEntities)
                {
                    try
                    {
                        if (target == null || target.EqualNetID(basementFoundation) || target is BuildingBlock || target.IsBasementEntity() || !IsBelowTerrain(target))
                        {
                            continue;
                        }
                        var kill = true;
                        if (target is BasePlayer basePlayer)
                        {
                            if (basePlayer.IsGod())
                            {
                                kill = false;
                            }
                        }
                        try
                        {
                            target.transform.position = target.transform.position.Set(y: GetTerrainHeightAtPosition(target.transform.position) + 2f);
                            target.SendNetworkUpdateImmediate();
                        }
                        catch (Exception)
                        {
                            // Could not move entity
                        }
                        if (kill)
                        {
                            target.Die();
                        }
                    }
                    catch (Exception)
                    {
                        // Failed to kill interior entity
                    }
                }
            }
        }

        void SpawnLadderHatchPair(BasementStructure basement, BuildingBlock surfaceFoundation, out BasementEntrance downHatch, out BasementEntrance upHatch)
        {
            Vector3 position = surfaceFoundation.transform.position + new Vector3(0f, 0.0f, 0f);
            Quaternion rotation = surfaceFoundation.transform.rotation;

            // Spawn down hatch
            var downdoor = SpawnBasementBlock(
                basement: basement, 
                prefab: Prefabs.LadderHatch, 
                parent: surfaceFoundation, 
                position: position, 
                rotation: rotation, 
                localRotation: new Vector3(0, 0, 180f),
                localPosition: new Vector3(0, 0.15f, 0f), 
                setParent: true, 
                buildingId: surfaceFoundation.buildingID,
                ownerid: surfaceFoundation.OwnerID) as Door;
            downHatch = new BasementEntrance
            {
                IsDownHatch = true,
                HatchId = downdoor.net.ID.Value
            };
            BasementEntrances[downHatch.HatchId] = downHatch;


            // Spawn up hatch
            var entitiesInRadius = GetEntitiesInRadius<BuildingBlock>(surfaceFoundation.transform.position.Set(y: basement.Elevation + 3f), 0.05f);
            var ceiling = entitiesInRadius.FirstOrDefault(x => x.ShortPrefabName.Contains("floor"));
            Pool<BuildingBlock>.Recycle(entitiesInRadius);
            var updoor= SpawnBasementBlock(
                basement: basement, 
                prefab: Prefabs.LadderHatch, 
                parent: ceiling, 
                position: ceiling.transform.position.Mod(y: -0.15f), 
                rotation: ceiling.transform.rotation, 
                setParent: true, 
                ownerid: 
                surfaceFoundation.OwnerID) as Door;
            upHatch = new BasementEntrance
            {
                IsDownHatch = false,
                HatchId = updoor.net.ID.Value
            };
            BasementEntrances[upHatch.HatchId] = upHatch;

            // Link hatches
            downHatch.ConnectedHatchId = upHatch.HatchId;
            upHatch.ConnectedHatchId = downHatch.HatchId;

            var effect = new Effect(Effects.PlaceFrame, downdoor, 0, Vector3.zero, Vector3.zero);
            effect.Play();
        }
        public BuildingBlock Spawn1x1BasementCell(BasementStructure basement, BuildingBlock surfaceFoundation)
        {
            var foundation = GetBasementFoundation(surfaceFoundation);
            if (foundation == null)
            {
                foundation = SpawnBasementBlock(basement, Prefabs.Foundation, surfaceFoundation, ownerid: surfaceFoundation.OwnerID) as BuildingBlock;
            }

            // Spawn walls if there isnt one
            for (int d = 1; d <= 4; d++)
            {
                // check if theres a foundation in that direction
                var adjFoundation = foundation.GetLinkedFoundation(d);
                if (adjFoundation == null)
                {
                    // spawn a wall if theres no foundation and no wall
                    var wall = foundation.GetLinkedWall(d, out Vector3 pos, out Vector3 rot);
                    if (wall == null)
                    {
                        SpawnBasementBlock(basement, Prefabs.Wall, foundation,
                            position: foundation.transform.position,
                            rotation: foundation.transform.rotation,
                            localPosition: pos,
                            localRotation: rot,
                            ownerid: foundation.OwnerID
                        );
                    }
                }
                else if (adjFoundation != null)
                {
                    // remove the wall in this direction if its a basement entity
                    var wall = foundation.GetLinkedWall(d, out Vector3 pos, out Vector3 rot);
                    if (wall != null && wall.IsBasementEntity())
                    {
                        KillBasementEntity(wall, basement, gibs: true);
                    }
                }
            }

            var ceiling = foundation.GetCeilingAboveFoundation();
            if (ceiling == null)
            {
                SpawnBasementBlock(basement, Prefabs.Floor, surfaceFoundation, localPosition: new Vector3(0f, 3f, 0f), localRotation: new Vector3(0, 0, 0), ownerid: foundation.OwnerID);
            }
            return foundation;
        }

        public void Spawn1x1BasementCellTriangle(BasementStructure basement, BuildingBlock surfaceFoundation)
        {
            var foundation = GetBasementFoundation(surfaceFoundation);
            if (foundation == null)
            {
                foundation = SpawnBasementBlock(basement, Prefabs.TriangleFoundation, surfaceFoundation) as BuildingBlock;
            }

            // Spawn walls if there isnt one
            for (int d = 1; d <= 3; d++)
            {
                // check if theres a foundation in that direction
                var adjFoundation = foundation.GetLinkedFoundation(d);
                if (adjFoundation == null)
                {
                    // spawn a wall if theres no foundation and no wall
                    var wall = foundation.GetLinkedWall(d, out Vector3 pos, out Vector3 rot);
                    if (wall == null)
                    {
                        SpawnBasementBlock(basement, Prefabs.Wall, foundation,
                            position: foundation.transform.position,
                            rotation: foundation.transform.rotation,
                            localPosition: pos,
                            localRotation: rot, 
                            ownerid: foundation.OwnerID
                        );
                    }
                }
            }

            var ceiling = foundation.GetCeilingAboveFoundation();
            if (ceiling == null)
            {
                SpawnBasementBlock(basement, Prefabs.TriangleFloor, surfaceFoundation, localPosition: new Vector3(0f, 3f, 0f), ownerid: foundation.OwnerID);
            }
        }

        public BaseEntity SpawnBasementBlock(BasementStructure basement, string prefab, StabilityEntity parent, Vector3? position = null, Quaternion? rotation = null, Vector3? localPosition = null, Vector3? localRotation = null, bool setParent = false, bool applyGrade = true, ulong ownerid = 0, uint? buildingId = null)
        {
            var entity = GameManager.server.CreateEntity(prefab, position ?? parent.transform.position.Set(y: basement.Elevation), rotation ?? parent.transform.rotation) as StabilityEntity;
            if (localPosition != null)
            {
                entity.transform.Translate(localPosition.Value);
            }
            if (localRotation != null)
            {
                entity.transform.Rotate(localRotation.Value);
            }
            if (entity is BuildingBlock block)
            {
                block.blockDefinition = PrefabAttribute.server.Find<Construction>(entity.prefabID);
                if (applyGrade)
                {
                    block.grade = config.BasementBuildingGrade;
                }
                else
                {
                    block.grade = BuildingGrade.Enum.Wood;
                }
            }
            entity.grounded = true;
            entity.OwnerID = ownerid;
            entity.AttachToBuilding(buildingId == null ? basement.BasementBuildingId : buildingId.Value);
            entity.Spawn();
            entity.Heal(10000f);
            if (setParent)
            {
                entity.SetParent(parent, worldPositionStays: true, sendImmediate: true);
            }
            BasementEntities.Add(entity.net.ID.Value);
            return entity;
        }

        public HashSet<ulong> CachedEntitiesBelowTerrain = new HashSet<ulong>();
        public bool IsBelowTerrain(BaseEntity entity)
        {
            if (entity == null || entity.net == null) { return false; }
            if (CachedEntitiesBelowTerrain.Contains(entity.net.ID.Value)) { return true; }
            Vector3 position = entity.transform.position;
            float terrainHeight = TerrainMeta.HeightMap.GetHeight(position);
            if (position.y < terrainHeight - 3f)
            {
                CachedEntitiesBelowTerrain.Add(entity.net.ID.Value);
                return true;
            }
            return false;
        }

        public BaseLock GetLockOnDoor(Door door)
        {
            BaseLock @lock = null;
            foreach (var child in door.children)
            {
                if (child is BaseLock)
                {
                    @lock = (BaseLock)child;
                    break;
                }
            }
            return @lock;
        }

        public BaseLock SpawnLockOnDoor(Door door, BaseLock clone, bool replaceOldLock = true)
        {
            if (replaceOldLock)
            {
                BaseLock oldLock = GetLockOnDoor(door);
                lockDestroyingInProgress = true;
                oldLock?.Kill();
                lockDestroyingInProgress = false;
            }
            BaseLock newLock = null;
            if (clone is CodeLock cloneCodeLock)
            {
                var newCodeLock = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab") as CodeLock;

                newCodeLock.gameObject.Identity();
                newCodeLock.SetParent(door, door.GetSlotAnchorName(BaseEntity.Slot.Lock));
                newCodeLock.Spawn();

                newCodeLock.code = cloneCodeLock.code;
                newCodeLock.hasCode = cloneCodeLock.hasCode;
                newCodeLock.guestCode = cloneCodeLock.guestCode;
                newCodeLock.hasGuestCode = cloneCodeLock.hasGuestCode;
                newCodeLock.whitelistPlayers = cloneCodeLock.whitelistPlayers;

                Effect.server.Run("assets/prefabs/locks/keypad/effects/lock-code-deploy.prefab",
                    newLock.transform.position);

                newLock = newCodeLock;
            }
            else if (clone is KeyLock cloneKeyLock)
            {
                var newKeyLock = GameManager.server.CreateEntity("assets/prefabs/locks/keylock/lock.key.prefab") as KeyLock;

                newKeyLock.gameObject.Identity();
                newKeyLock.SetParent(door, door.GetSlotAnchorName(BaseEntity.Slot.Lock));
                newKeyLock.Spawn();
                newKeyLock.keyCode = cloneKeyLock.keyCode;

                newLock = newKeyLock;
            }
            if (newLock != null)
            {
                newLock.SetFlag(BaseEntity.Flags.Locked, clone.HasFlag(BaseEntity.Flags.Locked));
                newLock.OwnerID = clone.OwnerID;
                door.SetSlot(BaseEntity.Slot.Lock, newLock);
            }

            return newLock;
        }

        public void GetBuildingBlocksBelowTerrain(float distance, List<BuildingBlock> blocksBelowTerrain)
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is BuildingBlock block)
                {
                    Vector3 position = block.transform.position;
                    float terrainHeight = TerrainMeta.HeightMap.GetHeight(position);
                    if (position.y < terrainHeight - distance)
                    {
                        blocksBelowTerrain.Add(block);
                    }
                }
            }
        }

        public bool IsWithinBasement(Vector3 position)
        {
            return RaycastFiltered(position, Vector3.up, 3f, out _, (hit) =>
            {
                return hit is BuildingBlock block && block.IsBasementEntity() && block.IsCeiling();
            });
        }
    }
}

namespace Oxide.Plugins.BasementMethods
{
    public static partial class ExtensionMethods
    {
        public static Basements.BasementEntrance AsBasementEntrance(this Door door) => door == null ? null : Basements.INSTANCE.BasementEntrances.GetValueOrDefault(door.net.ID.Value);
        public static bool IsBasement(this BuildingManager.Building basementBuilding) => basementBuilding == null ? false : Basements.INSTANCE.BasementStructures.ContainsKey(basementBuilding.ID);

        public static Basements.BasementStructure AsBasement(this BuildingManager.Building basementBuilding) => basementBuilding == null ? null : Basements.INSTANCE.BasementStructures.GetValueOrDefault(basementBuilding.ID);

        public static IEnumerable<Basements.BasementStructure> GetBasements(this BuildingManager.Building surfaceBuilding) => Basements.INSTANCE.GetBasementsConnectedToSurfaceBuilding(surfaceBuilding);

        public static BuildingBlock GetBasementFoundationFromSurface(this IEnumerable<Basements.BasementStructure> basements, BuildingBlock surfaceFoundation)
        {
            if (surfaceFoundation == null) { return null; }
            return basements.Select(x => GetBasementFoundationFromSurface(x.BasementBuilding, surfaceFoundation)).FirstOrDefault();
        }

        public static BuildingBlock GetBasementFoundationFromSurface(this BuildingManager.Building basementBuilding, BuildingBlock surfaceFoundation)
        {
            if (surfaceFoundation == null) { return null; }
            foreach (var basementBlock in basementBuilding?.buildingBlocks)
            {
                if (basementBlock != null && basementBlock.IsBasementEntity() && basementBlock.IsFoundation() && basementBlock.Equals2DPosition(surfaceFoundation))
                {
                    return basementBlock;
                }
            }
            return null;
        }

        public static BuildingBlock GetSurfaceFoundationFromBasement(this BuildingManager.Building surfaceBuilding, BuildingBlock basementFoundation)
        {
            if (basementFoundation == null) { return null; }
            foreach (var surfaceBlock in surfaceBuilding?.buildingBlocks)
            {
                if (surfaceBlock != null && surfaceBlock.IsFoundation() && surfaceBlock.Equals2DPosition(basementFoundation))
                {
                    return surfaceBlock;
                }
            }
            return null;
        }
    }
}

namespace Oxide.Plugins
{
    public partial class Basements
    {
        [Command("basements.getid"), Permission(PermissionAdmin)]
        private void CmdBasementsGetId(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null)
            {
                player.Message(Lang(player.Id, "command in game"));
                return;
            }
            var buildingblock = GetObjectRaycast(basePlayer, 100f) as BuildingBlock;
            if (buildingblock == null)
            {
                player.Message(Lang(player.Id, "command looking"));
                return;
            }
            var surfaceBuilding = buildingblock.GetBuilding();
            if (surfaceBuilding == null)
            {
                player.Message(Lang(player.Id, "command looking"));
                return;
            }
            var basements = GetBasementsConnectedToSurfaceBuilding(surfaceBuilding);
            if (basements?.Any() == false)
            {
                player.Message(Lang(player.Id, "command no basement"));
                return;
            }
            else
            {
                player.Message(Lang(player.Id, "command basements found", basements.Count, basements.Select(x => x.BasementBuildingId).ToSentence()));
                return;
            }
        }

        [Command("basements.destroy"), Permission(PermissionAdmin)]
        private void CmdBasementsDestroy(IPlayer player, string command, string[] args)
        {
            var usage = Lang(player.Id, "command usage", "basements.destroy <building id>");
            if (args.Length == 0)
            {
                player.Message(usage);
                return;
            }
            uint bid;
            if (!uint.TryParse(args[0], out bid))
            {
                player.Message(Lang(player.Id, "command invalid bid"));
                return;
            }
            var basement = BasementStructures.GetValueOrDefault(bid);
            if (basement == null)
            {
                player.Message(Lang(player.Id, "command no basement for id", bid));
            }
            try
            {
                DestroyBasement(basement);
            }
            catch (Exception)
            {
                player.Message(Lang(player.Id, "command failed to destroy", bid));
                return;
            }
            player.Message(Lang(player.Id, "command destroy success", bid));
        }


        [Command("basements.destroyall"), Permission(PermissionAdmin)]
        private void CmdBasementsDestroyAll(IPlayer player, string command, string[] args)
        {
            player.Message(Lang(player.Id, "command destroy all"));
            int destroyed = 0;
            UnsubscribeAll();
            foreach (var basement in BasementStructures.Values.ToArray())
            {
                try
                {
                    DestroyBasement(basement);
                    destroyed++;
                }
                catch (Exception)
                {
                    player.Message(Lang(player.Id, "command failed to destroy", basement?.BasementBuildingId));
                }
            }

            foreach (var entrance in BasementEntrances.Values)
            {
                entrance.BaseEntity?.Kill();
            }

            BasementStructures.Clear();
            BasementEntities.Clear();
            BasementEntrances.Clear();

            // Destroy any orphaned basements
            var blocksBelowTerrain = Pool<BuildingBlock>.Get();
            GetBuildingBlocksBelowTerrain(0.5f, blocksBelowTerrain);
            foreach (var block in blocksBelowTerrain)
            {
                try
                {
                    KillBasementEntity(block);
                } catch (Exception)
                {
                    // Ignore exception
                }
            }
            Pool<BuildingBlock>.Recycle(blocksBelowTerrain);
            player.Message(Lang(player.Id, "command destroy all success", destroyed));
            SubscribeAll();
        }

        [Command("basements.list"), Permission(PermissionAdmin)]
        private void CmdBasementsList(IPlayer player, string command, string[] args)
        {
            var msg = "BASEMENTS\n";
            foreach (var kvp in BasementStructures)
            {
                var basement = kvp.Value;
                basement.ClearCache();
                msg += $"KEY={kvp.Key} ID={basement.BasementBuildingId} ELE={basement.Elevation} DOORS {basement.Entrances?.Count() ?? 0}" + "\n";
            }

            msg += "\nENTRANCES\n";
            foreach (var kvp in BasementEntrances)
            {
                var entrance = kvp.Value;
                msg += $"KEY={kvp.Key} ID={entrance.HatchId} BASEMENT={entrance.BasementBuildingId} ISDOWN={entrance.IsDownHatch} Connected={entrance.ConnectedHatchId} ConnectedExists={entrance.ConnectedHatch != null}\n";
            }
            player.Message(msg);
        }
    }
}

namespace Oxide.Plugins
{
    public partial class Basements
    {
        private Configuration config;
        private class Configuration
        {
            [JsonProperty(PropertyName = "Basement Entrance Build Cost")]
            public string BasementEntranceBuildCost = "300 metal.fragments";

            [JsonProperty(PropertyName = "Basement Cell Build Cost (Square)")]
            public string BasementSquareCellBuildCost = "500 stones";

            [JsonProperty(PropertyName = "Basement Cell Build Cost (Triangle)")]
            public string BasementTriangleCellBuildCost = "250 stones";

            [JsonProperty(PropertyName = "Basement Cell Building Grade (Mostly Cosmetic)")]
            [JsonConverter(typeof(StringEnumConverter))]
            public BuildingGrade.Enum BasementBuildingGrade = BuildingGrade.Enum.Stone;

            [JsonProperty(PropertyName = "Required Foundation Grade For Basements")]
            [JsonConverter(typeof(StringEnumConverter))]
            public BuildingGrade.Enum RequiredFoundationGrade = BuildingGrade.Enum.Stone;

            [JsonProperty(PropertyName = "Max Basement Entrances Per Building")]
            public int EntranceLimit = 3;

            [JsonProperty(PropertyName = "Damage Required To Break Locks on Basement Entrances")]
            public float DamageRequiredToBreakLock = 50f;

            [JsonProperty(PropertyName = "Damage Required To Drill New Basement Cells")]
            public float DamageRequiredToDrillBasement = 5f;

            [JsonProperty(PropertyName = "Destroy Basement Cell When Surface Foundation is Destroyed")]
            public bool DestroyBasementUnderFoundation = true;

            [JsonProperty(PropertyName = "Allow Hatch Removal (requires other plugins)")]
            public bool AllowHatchRemoval = false;

            [JsonProperty(PropertyName = "Restrict Drill Items")]
            public bool RestrictDrillItems = true;

            [JsonProperty(PropertyName = "Sync Locks on Basement Entrances")]
            public bool SyncLocksOnBasementEntrances = true;

            [JsonProperty(PropertyName = "Allowed Items for Drilling (if Restrict Drill Items is true)")]
            public string[] BasementDrillItems = new string[]
            {
                "jackhammer"
            };

            [JsonProperty(PropertyName = "Forbidden Deployables in Basement (full prefab path)")]
            public string[] ForbiddenDeployablesInBasement = new string[]
            {
                "assets/prefabs/deployable/tool cupboard/shockbyte/cupboard.tool.shockbyte.deployed.prefab",
                "assets/prefabs/deployable/cupboard.tool.retro.deployed.prefab",
                "assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab"
            };

            [JsonProperty(PropertyName = "Chat Messages")]
            public ChatMessagesConfig ChatMessages = new ChatMessagesConfig
            {
                Enabled = true,
                IconSteamId = 0,
                Format = "{0}"
            };

            [JsonProperty(PropertyName = "Tool Cupboard UI")]
            public UIConfig ToolCupboardUI = new UIConfig
            {
                Enabled = true,
                Position = new CuiRectTransformComponent
                {
                    AnchorMin = "0.5 0",
                    AnchorMax = "0.5 0",
                    OffsetMin = $"{198f} {30f}",
                    OffsetMax = $"{198f+146f} {30f+32f}"
                }
            };

            [JsonProperty(PropertyName = "Basement Builder UI")]
            public UIConfig PlacementUI = new UIConfig
            {
                Enabled = true,
                Position = new CuiRectTransformComponent
                {
                    AnchorMin = $"0.5 0",
                    AnchorMax = $"0.5 0",
                    OffsetMin = $"{-(300f/2f)} {180f}",
                    OffsetMax = $"{(300f/2f)} {180f+52f}"
                }
            };

            [JsonProperty(PropertyName = "Insufficient Resources UI")]
            public UIConfig InsufficientResourcesUI = new UIConfig
            {
                Enabled = true,
                Position = new CuiRectTransformComponent
                {
                    AnchorMin = $"0.5 0",
                    AnchorMax = $"0.5 0",
                    OffsetMin = $"{-(300f / 2f)} {180f}",
                    OffsetMax = $"{(300f / 2f)} {180f + 52f}"
                }
            };
        }

        public class ChatMessagesConfig
        {
            public bool Enabled;
            public ulong IconSteamId;
            public string Format;
        }

        public class UIConfig
        {
            public bool Enabled;
            public CuiRectTransformComponent Position;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
            }
            catch (Exception e)
            {
                throw e;
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
            SaveConfig();

            // Do parsings
            BasementBuildCostSquare = ItemCost.Parse(config.BasementSquareCellBuildCost);
            BasementBuildCostTriangle = ItemCost.Parse(config.BasementTriangleCellBuildCost);
            BasementEntranceBuildCost = ItemCost.Parse(config.BasementEntranceBuildCost);
            BasementDrillItems = config.BasementDrillItems.Select(x => ItemManager.FindItemDefinition(x)).ToArray();
        }
        protected override void SaveConfig() => Config.WriteObject(config);
        protected override void LoadDefaultConfig() => config = new Configuration();
    }
}

namespace Oxide.Plugins
{
    public partial class Basements
    {
        public static class Prefabs
        {
            public const string Foundation = "assets/prefabs/building core/foundation/foundation.prefab";
            public const string TriangleFoundation = "assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab";
            public const string Wall = "assets/prefabs/building core/wall/wall.prefab";
            public const string Floor = "assets/prefabs/building core/floor/floor.prefab";
            public const string TriangleFloor = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab";
            public const string LadderHatch = "assets/prefabs/building/floor.ladder.hatch/floor.ladder.hatch.prefab";
        }

        public static class Effects
        {
            public const string PlaceFrame = "assets/bundled/prefabs/fx/build/frame_place.prefab";
            public const string PromoteStone = "assets/bundled/prefabs/fx/build/promote_stone.prefab";
            public const string MetalImpact = "assets/bundled/prefabs/fx/impacts/bullet/metal/metal1.prefab";
            public const string Gibs = "assets/bundled/prefabs/fx/entities/loot_barrel/gib.prefab";
            public const string LootCopy = "assets/bundled/prefabs/fx/notice/loot.copy.fx.prefab";
            public const string LandmineTrigger = "assets/bundled/prefabs/fx/weapons/landmine/landmine_trigger.prefab";
        }
    }
}

namespace Oxide.Plugins.BasementMethods
{
    public static partial class ExtensionMethods
    {
        public static Vector3 Mod(this Vector3 value, float x = 0, float y = 0, float z = 0)
        {
            return value + new Vector3(x, y, z);
        }

        public static Vector3 Set(this Vector3 value, float? x = null, float? y = null, float? z = null)
        {
            return new Vector3(x ?? value.x, y ?? value.y, z ?? value.z);
        }

        public static Basements.BasementStructure GetBasement(this DecayEntity block)
        {
            var buildingId = block.buildingID;
            if (buildingId == 0) { return null; }
            var basement = Basements.INSTANCE.BasementStructures.GetValueOrDefault(buildingId);
            if (basement != null)
            {
                return basement; // Found the basement
            }
            var surfaceBuilding = BuildingManager.server.GetBuilding(buildingId);
            if (surfaceBuilding == null) { return null; }
            basement = Basements.INSTANCE.GetBasementsConnectedToSurfaceBuilding(surfaceBuilding).FirstOrDefault();
            return basement;
        }

        public static bool IsBasementEntity(this BaseEntity entity)
        {
            if (entity == null) { return false; }
            return Basements.INSTANCE.BasementEntities.Contains(entity.net.ID.Value);
        }

        public static bool IsBasementEntrance(this BaseEntity entity)
        {
            if (entity == null) { return false; }
            return Basements.INSTANCE.BasementEntrances.ContainsKey(entity.net.ID.Value);
        }

        public static bool IsBasementEntrance(this BaseEntity entity, out Basements.BasementEntrance entrance)
        {
            entrance = Basements.INSTANCE.BasementEntrances.GetValueOrDefault(entity.net.ID.Value);
            return entrance != null;
        }

        public static void Play(this Effect effect)
        {
            EffectNetwork.Send(effect);
        }

        public static void Play(this Effect effect, BasePlayer basePlayer)
        {
            EffectNetwork.Send(effect, basePlayer.Connection);
        }

        public static void Notify(this BasePlayer basePlayer, string messageId, params object[] args) => Basements.INSTANCE.Message(basePlayer, true, messageId, args);

        public static void NotifyIgnoreCooldown(this BasePlayer basePlayer, string messageId, params object[] args) => Basements.INSTANCE.Message(basePlayer, false, messageId, args);

        public static void Teleport(this BaseEntity entity, float x = 0, float y = 0, float z = 0)
        {
            entity.transform.position = entity.transform.position.Mod(x, y, z);
            entity.SendNetworkUpdateImmediate(justCreated: true);
        }

        public static bool IsNetNull(this BaseEntity baseEntity) => baseEntity == null || baseEntity.net == null;

        public static BuildingBlock GetLinkedFoundationForWall(this BuildingBlock block)
        {
            foreach (var link in block.links)
            {
                if (link == null || link.socket == null || !link.socket.socketName.Contains("wall-male")) { continue; }
                foreach (var connection in link.connections)
                {
                    if (connection == null) { continue; }
                    var owner = connection.owner as BuildingBlock;
                    if (owner != null && owner.ShortPrefabName.Contains("foundation"))
                    {
                        return owner;
                    }
                }
            }
            return null;
        }

        public static BuildingBlock GetLinkedFoundation(this BuildingBlock block, int dir)
        {
            foreach (var link in block.links)
            {
                if (link == null || link.socket == null || !link.socket.socketName.Contains("foundation-mid") || !link.socket.socketName.EndsWith(dir.ToString())) { continue; }
                foreach (var connection in link.connections)
                {
                    if (connection == null) { continue; }
                    var owner = connection.owner as BuildingBlock;
                    if (owner != null && owner.ShortPrefabName.Contains("foundation"))
                    {
                        return owner;
                    }
                }
            }
            return null;
        }

        public static BuildingBlock GetCeiling(this BuildingBlock block, float radius = 0.005f)
        {
            var ceilingpos = block.transform.position.Mod(y: 3f);
            var nearby = Basements.INSTANCE.GetEntitiesInRadius<BuildingBlock>(ceilingpos, radius);
            var ceiling = nearby.FirstOrDefault(x => x.IsCeiling() && x.IsTriangle() == block.IsTriangle());
            Basements.Pool<BuildingBlock>.Recycle(nearby);
            return ceiling;
        }

        public static BuildingBlock GetCeilingAboveFoundation(this BuildingBlock foundation)
        {
            Vector3 raycastStart = foundation.WorldSpaceBounds().ToBounds().center.Set(y: foundation.transform.position.y);
            float raycastDistance = 3.1f;

            Ray ray = new Ray(raycastStart, Vector3.up);

            var hits = Physics.RaycastAll(ray, raycastDistance, LayerMask.GetMask("Construction"));
            foreach (var hit in hits)
            {
                BaseEntity hitEntity = hit.GetEntity();
                if (hitEntity is BuildingBlock ceiling && ceiling.PrefabName.Contains("floor"))
                {
                    return ceiling;
                }
            }
            return null;
        }

        public static BuildingBlock GetLinkedWall(this BuildingBlock block, int dir, out Vector3 position, out Vector3 rotation)
        {
            position = Vector3.zero;
            rotation = Vector3.zero;
            var triangle = block.IsTriangle();
            foreach (var link in block.links)
            {
                if (link == null || link.socket == null || !(link.socket.socketName.Contains("wall-female") || (link.socket.socketName.Contains("wall") && link.socket.socketName.Contains("female"))) || !link.socket.socketName.EndsWith(dir.ToString())) { continue; }
                position = link.socket.localPosition;
                if (dir == 1)
                {
                    rotation = triangle ? new Quaternion(0f, 0.70711f, 0f, 0.70711f).eulerAngles : new Vector3(0, 90, 0);
                }
                else if (dir == 2)
                {
                    rotation = triangle ? new Quaternion(0f, -0.96593f, 0f, 0.25882f).eulerAngles : new Vector3(0, 180, 0);
                }
                else if (dir == 3)
                {
                    rotation = triangle ? new Quaternion(0f, 0.25882f, 0f, -0.96593f).eulerAngles : new Vector3(0, 270, 0);
                }


                foreach (var connection in link.connections)
                {
                    if (connection == null) { continue; }
                    var owner = connection.owner as BuildingBlock;
                    if (owner != null && owner.ShortPrefabName.Contains("wall"))
                    {
                        return owner;
                    }
                }
            }
            return null;
        }

        public static bool IsFoundation(this BuildingBlock block) => block?.ShortPrefabName.Contains("foundation") ?? false;

        public static bool IsTriangle(this BuildingBlock block) => block?.ShortPrefabName.Contains("triangle") ?? false;

        public static bool IsWall(this BuildingBlock block) => block?.ShortPrefabName.Contains("wall") ?? false;

        public static bool IsCeiling(this BuildingBlock block) => block?.ShortPrefabName.Contains("floor") ?? false;

        public static bool Equals2DPosition(this BuildingBlock block, BuildingBlock other) => block.transform.position.x == other.transform.position.x && block.transform.position.z == other.transform.position.z;
    }
}

namespace Oxide.Plugins
{
    public partial class Basements
    {
        private static bool EqualsPosition2D(Vector3 a, Vector3 b, float tolerance = 0.01f)
        {
            return Mathf.Abs(a.x - b.x) < tolerance && Mathf.Abs(a.z - b.z) < tolerance;
        }
        private static BaseEntity GetObjectRaycast(BasePlayer basePlayer, float distance)
        {
            Ray ray = new Ray(basePlayer.eyes.position, basePlayer.eyes.HeadForward());
            RaycastHit hit;
            return !Physics.Raycast(ray, out hit, distance) ? null : hit.GetEntity();
        }

        public static BaseEntity FindBaseEntity(ulong entityID)
        {
            foreach (BaseEntity entity in BaseNetworkable.serverEntities)
            {
                if (entity == null) { continue; }
                if (entityID == entity.net.ID.Value)
                {
                    return entity;
                }
            }
            return null;
        }

        public List<T> GetEntitiesInRadius<T>(Vector3 position, float dist) where T : BaseEntity
        {
            var list = Pool<T>.Get();
            Vis.Entities(position, dist, list);
            return list;
        }

        public int GetInventoryItemAmount(BasePlayer basePlayer, string itemShortName)
        {
            var count = 0;
            reusableItemsList.Clear();
            basePlayer.inventory.GetAllItems(reusableItemsList);
            foreach (var item in reusableItemsList)
            {
                if (item.info.shortname == itemShortName)
                {
                    count += item.amount;
                }
            }
            return count;
        }

        public static float GetTerrainHeightAtPosition(Vector3 position)
        {
            return TerrainMeta.HeightMap.GetHeight(position);
        }

        public static BaseEntity RaycastVertical(BaseEntity start, Vector3? dir = null, float distance = 3f, Func<BaseEntity, bool> cond = null)
        {
            if (dir == null)
            {
                dir = Vector3.down;
            }
            Vector3 raycastStart = start.WorldSpaceBounds().ToBounds().center.Set(y: start.transform.position.y);
            Ray ray = new Ray(raycastStart, dir.Value);
            var hits = Physics.RaycastAll(ray, distance, LayerMask.GetMask("Construction"));
            foreach (var hit in hits)
            {
                BaseEntity hitEntity = hit.GetEntity();
                if (hitEntity != null && (cond?.Invoke(hitEntity) ?? true))
                {
                    return hitEntity;
                }
            }
            return null;
        }

        private List<Item> reusableItemsList = new List<Item>();

        public void UseInventoryItemAmount(BasePlayer basePlayer, string itemShortName, double amount)
        {
            var due = amount;
            ItemDefinition itemDef = null;
            reusableItemsList.Clear();
            basePlayer.inventory.GetAllItems(reusableItemsList);
            foreach (var item in reusableItemsList)
            {
                if (due <= 0)
                {
                    break;
                }
                if (item.info.shortname == itemShortName)
                {
                    if (itemDef == null)
                    {
                        itemDef = item.info;
                    }
                    if (item.amount >= due)
                    {
                        item.UseItem((int)Math.Ceiling(due));
                        due = 0;
                    }
                    else
                    {
                        due -= item.amount;
                        item.UseItem(item.amount);
                    }
                }
            }
            if (itemDef != null && amount > 0)
            {
                basePlayer.Command("note.inv", itemDef.itemid, -((int)Math.Ceiling(amount)));
            }
        }

        private Dictionary<string, float> NotifyDelays = new Dictionary<string, float>();
        private const float ChatCooldown = 4f;
        public void Message(BasePlayer basePlayer, bool delay, string message, params object[] args)
        {
            if (!config.ChatMessages.Enabled) { return; }
            if (delay)
            {
                var userid = basePlayer.UserIDString;
                var key = CompoundKey(userid, message);
                if (ChatCooldown > 0 && NotifyDelays.GetValueOrDefault(key) > UnityEngine.Time.realtimeSinceStartup) { return; }
                NotifyDelays[key] = UnityEngine.Time.realtimeSinceStartup + ChatCooldown;
            }
            var text = string.Format(config.ChatMessages.Format, Lang(basePlayer, message, args));
            ConsoleNetwork.SendClientCommand(basePlayer.Connection, "chat.add", 2, config.ChatMessages.IconSteamId, text);
        }

        private static string CompoundKey(object obj1, object obj2) => $"{obj1}|{obj2}";

        internal RaycastHit[] reusableHits = new RaycastHit[10];
        public static bool RaycastFiltered(Vector3 startPos, Vector3 direction, float distance, out BaseEntity firstHit, Func<BaseEntity, bool> filter)
        {
            firstHit = null;
            int hitCount = Physics.RaycastNonAlloc(startPos, direction, INSTANCE.reusableHits, distance);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = INSTANCE.reusableHits[i];
                BaseEntity hitEntity = hit.GetEntity();
                if (hitEntity != null && filter.Invoke(hitEntity))
                {
                    firstHit = hitEntity;
                    return true;
                }
            }
            return false;
        }

        internal RaycastHit[] reusableSpherecast = new RaycastHit[100];
        public static bool SpherecastFiltered(Vector3 startPos, float radius, out BaseEntity firstHit, Func<BaseEntity, bool> filter)
        {
            firstHit = null;
            var hitCount = Physics.SphereCastNonAlloc(startPos, radius, Vector3.up, INSTANCE.reusableSpherecast);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = INSTANCE.reusableSpherecast[i];
                BaseEntity hitEntity = hit.GetEntity();
                if (hitEntity != null && filter.Invoke(hitEntity))
                {
                    firstHit = hitEntity;
                    return true;
                }
            }
            return false;
        }

        public static class Pool<T>
        {
            private static readonly Stack<List<T>> _stack = new Stack<List<T>>();

            public static List<T> Get()
            {
                return _stack.Count > 0 ? _stack.Pop() : new List<T>();
            }

            public static void Recycle(List<T> list)
            {
                list.Clear();
                _stack.Push(list);
            }
        }
    }
}

namespace Oxide.Plugins
{
    public partial class Basements
    {
        [PluginReference]
        private Plugin BuildTools;

        // RemoverTool | BuildTools
        private HashSet<ulong> AllowRemoval = new HashSet<ulong>();
        private object canRemove(BasePlayer basePlayer, BaseEntity entity)
        {
            if (basePlayer == null || entity == null) { return null; }
            if (PlacementPlayers.ContainsKey(basePlayer.UserIDString))
            {
                return false;
            }
            if (entity is Door door && entity.IsBasementEntity() && config.AllowHatchRemoval)
            {
                var basement = door?.AsBasementEntrance()?.Basement;
                var basementExists = basement != null;
                var basementId = basement?.BasementBuildingId;
                AllowRemoval.Add(door.net.ID.Value);
                timer.In(0.2f, () =>
                {
                    if (basementId.HasValue == false) { return; }
                    var basement = BasementStructures.GetValueOrDefault(basementId.Value);
                    if (basePlayer == null || !basementExists) { return; }
                    if (basement == null)
                    {
                        basePlayer?.SendConsoleCommand("gametip.showtoast", 1, Lang(basePlayer, "toast basement destroyed"), string.Empty);
                    }
                });
            }
            else if (entity.IsBasementEntity())
            {
                return false;
            }
            return null;
        }


        // BaseRepair
        private object CanBaseRepair(BasePlayer basePlayer)
        {
            if (basePlayer == null) { return null; }
            var session = PlacementPlayers.GetValueOrDefault(basePlayer.UserIDString);
            if (session == null) { return null; }
            return false; // prevent base repair
        }


        // TruePVE
        private object CanEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            var block = hitinfo?.HitEntity as BuildingBlock;
            if (block?.IsBasementEntity() == true)
            {
                return true; // Allow damage so that Basements and reject it if needed
            }
            return null;
        }

        // BuildTools
        private object canDowngrade(BasePlayer basePlayer, BuildingBlock entity)
        {
            if (entity?.IsBasementEntity() == true)
            {
                return true; // Prevent downgrade
            }
            return null;
        }

        public void ForceCloseBuildTools(BasePlayer basePlayer)
        {
            BuildTools?.CallHook("CloseBuildingMode", basePlayer, (Item)null);
        }

        // PocketDimensions
        [PluginReference]
        private Plugin PocketDimensions;

        public bool IsInPocketDimension(DecayEntity decayEntity)
        {
            return PocketDimensions?.Call("CheckBelongsToDimensionalPocket", decayEntity).Equals(true) ?? false;
        }

    }
}

namespace Oxide.Plugins
{
    public partial class Basements
    {
        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["placement error foundation"] = "The basement entrance needs to be placed on a foundation.",
                ["placement error grade"] = "The foundation needs to be upgraded to at least {0} for a basement.",
                ["placement error in basement"] = "You can not place an entrance within the basement.",
                ["placement error space"] = "There is no room on this foundation for an entrance. Make sure there are no deployables or obstructions on this foundation.",
                ["placement error basement space"] = "There is no room in the basement under this foundation for an entrance.",
                ["placement error cost"] = "You need {0} in order to build a basement for your base size.",
                ["placement error basement"] = "There is no basement structure under this location to place an entrance.",
                ["placement error building"] = "The basement entrance must be part of the building with your tool cupboard.",
                ["placement error exists"] = "There already exists a basement for this building.",
                ["placement error terrain"] = "This terrain is not suitable for a basement. Try moving away from cliffs, rocks, junk piles or railroad tunnels.",
                ["placement error tc"] = "You cannot place tool cupboards in basements.",
                ["build basement success"] = "Basement built. If this entrance is destroyed then the entire basement will be destroyed.",
                ["cannot modify basement"] = "You cannot damage, demolish or upgrade the exterior walls of basements.",
                ["cannot build near basement"] = "You cannot build that close to a basement entrance.",
                ["cannot drill without auth"] = "You cannot expand this basement without building privilege.",
                ["ui add basement door"] = "ADD BASEMENT DOOR",
                ["ui add a basement"] = "ADD A BASEMENT",
                ["ui cannot build basement"] = "CANNOT BUILD BASEMENT",
                ["ui at max doors"] = "AT MAX BASEMENT DOORS",
                ["ui cancel placement"] = "CANCEL PLACEMENT",
                ["ui insufficient resources"] = "INSUFFICIENT RESOURCES",
                ["ui hit a foundation"] = "Hit a foundation with a hammer to place entrance.",
                ["ui basement builder"] = "BASEMENT BUILDER",
                ["help tool required"] = "A {0} is required to expand the basement.",
                ["help basement expand"] = "Use a {0} on the walls to expand your basement.",
                ["help no foundation above"] = "There is no suitable foundation above that area.",
                ["help foundation grade"] = "There is no suitable foundation above that area. The foundation must be at least {0} tier.",
                ["help bad elevation"] = "The above foundation is too low elevation for a basement.",
                ["help mismatch elevation"] = "You cannot combine basements with differing elevations.",
                ["toast door count"] = "Basement Doors {0}/{1}",
                ["toast basement built"] = "Basement Built",
                ["toast basement destroyed"] = "Basement Destroyed",
                ["toast cannot place in basement"] = "Cannot place in basement",
                ["command in game"] = "This command must be used in game.",
                ["command looking"] = "You must be looking at a building block. Try moving closer.",
                ["command bid"] = "The building ID is <color=yellow>{0}</color> for that structure.",
                ["command no basement"] = "This building has no basement attached to it.",
                ["command basements found"] = "This building has {0} basement(s) attached to it with the following IDs: {1}",
                ["command has basement"] = "This building has a basement with {0} entrance(s).",
                ["command usage"] = "Usage: {0}",
                ["command no basement for id"] = "No basement exists with the ID of {0}.",
                ["command invalid bid"] = "Invalid building ID. Use /basements.getid for a valid ID of a building.",
                ["command basement not found"] = "No basement found for the building with ID {0}. It may have been destroyed already.",
                ["command failed to destroy"] = "Failed to destroy basement with building ID {0}.",
                ["command destroy success"] = "Successfully destroyed basement with building ID {0}.",
                ["command destroy partial success"] = "The building with ID {0} did not have a basement, but there were basement entities below it that are now destroyed.",
                ["command destroy all"] = "Destroying all basements, this might cause lag for a moment..",
                ["command destroy all success"] = "Successfully destroyed {0} basement(s)."
            }, this);
        }

        private static string Lang(BasePlayer basePlayer, string key, params object[] args) => string.Format(INSTANCE.lang.GetMessage(key, INSTANCE, basePlayer.UserIDString), args);

        private static string Lang(string userid, string key, params object[] args) => string.Format(INSTANCE.lang.GetMessage(key, INSTANCE, userid), args);
        #endregion
    }
}

namespace Oxide.Plugins
{
    public partial class Basements
    {
        public class BasementBuildSession
        {
            public uint BuildingId;
            public BuildingBlock HitBlock;
            public bool HasEquippedHammer = false;
            public BuildingPrivlidge BuildingPrivlidge;
        }

        public struct ItemCost
        {
            public string ItemShortName;
            public double Amount;

            public ItemDefinition ItemDef;

            public static ItemCost Parse(string itemCostStr)
            {
                try
                {
                    var split = itemCostStr.Trim().ToLower().Split();
                    var amt = double.Parse(split[0]).Clamp(1, 99999);
                    var itemshortname = split[1];
                    var itemDef = ItemManager.FindItemDefinition(itemshortname);
                    return new ItemCost
                    {
                        Amount = amt,
                        ItemShortName = itemshortname,
                        ItemDef = itemDef
                    };
                } 
                catch(Exception)
                {
                    INSTANCE.PrintError($"Invalid item cost given '{itemCostStr}'");
                    return new ItemCost
                    {
                        Amount = 0,
                        ItemShortName = "",
                        ItemDef = null
                    };
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    public partial class Basements
    {

        public Dictionary<ulong, HashSet<string>> PlayersLootingTc = new Dictionary<ulong, HashSet<string>>();
        void OnLootEntity(BasePlayer basePlayer, BaseEntity entity)
        {
            if (basePlayer == null || entity == null || entity.net == null || !(entity is BuildingPrivlidge priv)) { return; }
            PlayersLootingTc.GetOrCreate(entity.net.ID.Value).Add(basePlayer.UserIDString);
            ShowToolcupboardButton(basePlayer, priv);
        }

        void OnLootEntityEnd(BasePlayer basePlayer, BaseCombatEntity entity)
        {
            if (entity == null || !(entity is BuildingPrivlidge)) { return; }
            PlayersLootingTc.GetValueOrDefault(entity.net.ID.Value)?.Remove(basePlayer.UserIDString);
            DestroyToolcupboardButton(basePlayer);
        }

        void OnActiveItemChanged(BasePlayer basePlayer, Item oldItem, Item newItem)
        {
            if (basePlayer == null) { return; }
            var session = PlacementPlayers.GetValueOrDefault(basePlayer.UserIDString);
            if (session != null)
            {
                ShowPlacementBanner(basePlayer);
            }
        }

        void OnHammerHit(BasePlayer basePlayer, HitInfo info)
        {
            if (basePlayer == null) { return; }
            DestroyInsufficientResources(basePlayer);
            var session = PlacementPlayers.GetValueOrDefault(basePlayer.UserIDString);
            if (session != null)
            {
                if (BuildTools?.IsLoaded == true) // Force close build tools
                {
                    ForceCloseBuildTools(basePlayer);
                }
                var block = info.HitEntity as BuildingBlock;
                if (block == null) { return; }
                session.HitBlock = block;
                var basement = block.GetBasement();
                var basementExists = basement != null;
                if (!permission.UserHasPermission(basePlayer.UserIDString, PermissionBuild))
                {
                    // No permission, end the placement
                    EndPlacement(basePlayer, block?.GetBuildingPrivilege());
                }
                else if (TryPlacement(basePlayer, basement, block, session))
                {
                    // Attempt to build an entrance
                    var isFree = permission.UserHasPermission(basePlayer.UserIDString, PermissionFree);
                    var amountInInv = isFree ? 0 : GetInventoryItemAmount(basePlayer, BasementEntranceBuildCost.ItemShortName);
                    if (!isFree && amountInInv < BasementEntranceBuildCost.Amount)
                    {
                        // Cant afford
                        ShowInsufficientResources(basePlayer, BasementEntranceBuildCost.ItemDef, amountInInv, (int)Math.Ceiling(BasementEntranceBuildCost.Amount));
                    }
                    else
                    {
                        // Can afford
                        basement = SpawnBasement(block);
                        if (!basementExists)
                        {
                            // Add new basement
                            basePlayer.SendConsoleCommand("gametip.showtoast", 3, Lang(basePlayer, "toast basement built"), string.Empty);
                        }
                        else
                        {
                            // Add to existing basement
                            NextTick(() =>
                            {
                                if (basePlayer != null && basement != null && config != null)
                                {
                                    basePlayer.SendConsoleCommand("gametip.showtoast", 0, Lang(basePlayer, "toast door count", basement.EntrancePairCount + 1, config.EntranceLimit), string.Empty);
                                }
                            });
                        }
                        if (!isFree)
                        {
                            UseInventoryItemAmount(basePlayer, BasementEntranceBuildCost.ItemShortName, BasementEntranceBuildCost.Amount);
                        }
                        NextTick(() =>
                        {
                            EndPlacement(basePlayer, block?.GetBuildingPrivilege());
                        });
                    }
                }
            }
        }

        object OnStructureRepair(BuildingBlock block, BasePlayer basePlayer)
        {
            if (basePlayer == null) { return null; }
            var session = PlacementPlayers.GetValueOrDefault(basePlayer.UserIDString);
            if (session != null)
            {
                EndPlacement(basePlayer, block?.GetBuildingPrivilege());
                return false; // Prevent repair while session is active
            }
            return null;
        }

        public bool lockUpdatingInProgress = false;
        void CanChangeCode(BasePlayer basePlayer, CodeLock baseLock, string newCode, bool isGuestCode)
        {
            if (lockUpdatingInProgress == true) { return; }
            // Sync Locks - Update Other Lock Code
            NextTick(() =>
            {
                if (basePlayer == null || baseLock == null) { return; }
                SyncLockHelper(baseLock);
            });
        }

        void CanLock(BasePlayer basePlayer, BaseLock baseLock)
        {
            if (lockUpdatingInProgress == true) { return; }
            // Sync Locks - Lock/Unlock
            NextTick(() =>
            {
                if (baseLock == null) { return; }
                SyncLockHelper(baseLock);
            });
        }

        void CanUnlock(BasePlayer basePlayer, BaseLock baseLock)
        {
            if (lockUpdatingInProgress == true) { return; }
            // Sync Locks - Lock/Unlock
            NextTick(() =>
            {
                if (baseLock == null) { return; }
                SyncLockHelper(baseLock);
            });
        }

        private void SyncLockHelper(BaseLock baseLock)
        {
            var parent = baseLock.GetParentEntity();
            if (parent != null && parent is Door parentDoor && parentDoor.IsBasementEntrance(out BasementEntrance entrance))
            {
                var otherEntrance = entrance.ConnectedHatch;
                if (otherEntrance != null && otherEntrance.BaseEntity != null)
                {
                    lockUpdatingInProgress = true;
                    try
                    {
                        var otherLock = GetLockOnDoor(otherEntrance.BaseEntity);
                        if (otherLock != null)
                        {
                            if (otherLock is CodeLock otherCodeLock && baseLock is CodeLock baseCodeLock)
                            {
                                otherCodeLock.code = baseCodeLock.code;
                                otherCodeLock.guestCode = baseCodeLock.guestCode;
                                otherCodeLock.whitelistPlayers = baseCodeLock.whitelistPlayers;
                            }
                            if (otherLock is KeyLock otherKeyLock && baseLock is KeyLock baseKeyLock)
                            {
                                otherKeyLock.keyCode = baseKeyLock.keyCode;
                            }
                            otherLock.SetFlag(BaseEntity.Flags.Locked, baseLock.HasFlag(BaseEntity.Flags.Locked));
                        }
                    }
                    catch (Exception) { }
                    lockUpdatingInProgress = false;
                }
            }
        }

        bool destroying = false;
        bool lockDestroyingInProgress = false;
        object OnEntityKill(BaseEntity entity)
        {
            if (entity == null || entity.net == null || entity.IsDestroyed) { return null; }
            var entityid = entity.net.ID.Value;

            // Sync Locks - Destroy Other Lock
            if (lockDestroyingInProgress == false && config.SyncLocksOnBasementEntrances && entity is BaseLock baseLock && baseLock != null)
            {
                var parent = baseLock.GetParentEntity();
                if (parent != null && parent is Door parentDoor && parentDoor.IsBasementEntrance(out BasementEntrance entrance))
                {
                    var otherEntrance = entrance.ConnectedHatch;
                    if (otherEntrance != null && otherEntrance.BaseEntity != null)
                    {
                        lockDestroyingInProgress = true;
                        try
                        {
                            BaseLock otherLock = GetLockOnDoor(otherEntrance.BaseEntity);
                            otherLock?.Kill();
                        } catch (Exception) { }
                        lockDestroyingInProgress = false;
                        return null;
                    }
                }
            }

            // Prevent destroying basement entities
            if (AllowRemoval.Contains(entityid))
            {
                AllowRemoval.Remove(entityid);
                destroying = true;
            }
            if (entity.IsBasementEntity() && destroying == false)
            {
                return false; // Basement entities cannot be destroyed
            }

            // Surface cell destroyed, destroy basement cell
            if (entity is BuildingBlock block && !block.IsNetNull() && block.IsFoundation() && !block.IsBasementEntity())
            {
                var surfaceFoundation = block;

                // Kill entrance that is attached to surface foundation
                if (surfaceFoundation.children.Any(x => (x as Door)?.IsBasementEntrance() == true))
                {
                    var door2 = block.children.FirstOrDefault(x => x is Door) as Door;
                    var entrance = door2.AsBasementEntrance();
                    DestroyBasementEntrancePair(entrance);
                    KilledEntity = block.net.ID.Value;
                }

                if (config.DestroyBasementUnderFoundation)
                {
                    var surfaceBuilding = surfaceFoundation.GetBuilding();
                    if (surfaceBuilding != null)
                    {
                        var basements = surfaceBuilding.GetBasements();
                        var basementFoundation = basements.GetBasementFoundationFromSurface(surfaceFoundation);
                        // Kill basement foundation if surface foundation is destroyed
                        if (basementFoundation != null)
                        {
                            var basement = basementFoundation.GetBuilding().AsBasement();
                            DestroyBasementCell(basementFoundation);
                            basement.ClearCache();
                            SpawnMissingExteriorWalls(basement);
                            return null;
                        }
                    }
                }
            }

            // Entrance destroyed, destroy other entrance
            if (entity is Door door && !door.IsNetNull() && door.IsBasementEntrance())
            {
                var entrance = BasementEntrances[door.net.ID.Value];
                var basement = entrance.Basement;
                BasementEntrances.Remove(entrance.HatchId);
                if (entrance.ConnectedHatch != null)
                {
                    entrance.ConnectedHatch.BaseEntity.Kill();
                }
                if (basement != null)
                {
                    basement.ClearCache();
                    if (basement.Entrances.Any() == false)
                    {
                        DestroyBasement(basement);
                    }
                }
                return null;
            }
            return null;
        }

        private ulong? KilledEntity = null;
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null || info.InitiatorPlayer == null || !(entity is BuildingBlock)) { return; }
            var entityid = entity.net.ID.Value;
            NextTick(() =>
            {
                if (KilledEntity != null && KilledEntity.Value == entityid)
                {
                    info.InitiatorPlayer?.SendConsoleCommand("gametip.showtoast", 1, Lang(info.InitiatorPlayer, "toast basement destroyed"), string.Empty);
                    KilledEntity = null;
                }
            });
        }

        void OnStructureDemolish(BaseCombatEntity entity, BasePlayer basePlayer, bool immediate)
        {
            if (entity == null || basePlayer == null || !(entity is BuildingBlock)) { return; }
            var entityid = entity.net.ID.Value;
            NextTick(() =>
            {
                if (KilledEntity != null && KilledEntity.Value == entityid)
                {
                    basePlayer?.SendConsoleCommand("gametip.showtoast", 1, Lang(basePlayer, "toast basement destroyed"), string.Empty);
                    KilledEntity = null;
                }
            });
        }

        Dictionary<ulong, float> DamageDoneToDoors = new Dictionary<ulong, float>();

        Dictionary<ulong, float> DamageDoneToWalls = new Dictionary<ulong, float>();

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null || entity.IsDestroyed) { return null; }
            else if (entity.IsBasementEntity())
            {
                var showmsg = true;
                // Handle decay
                if (info.damageTypes.Has(Rust.DamageType.Decay))
                {
                    showmsg = false;
                    goto End;
                }
                // Handle door damage with locks
                else if (entity is Door door)
                {
                    var damage = info.damageTypes.Total();

                    // Damage toward lock
                    var doorlock = door.children.FirstOrDefault(x => x is BaseLock) as BaseLock;
                    if (doorlock != null)
                    {
                        var curval = DamageDoneToDoors.GetOrCreate(door.net.ID.Value);
                        curval += damage;
                        if (damage >= 1f)
                        {
                            var sfx1 = new Effect(Effects.MetalImpact, doorlock, 0, Vector3.zero, Vector3.zero);
                            sfx1.Play();
                        }
                        if (curval >= config.DamageRequiredToBreakLock)
                        {
                            // Door taking damage, destroy the lock
                            var isExplosiveDamange = info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Explosion;
                            if (isExplosiveDamange)
                            {
                                var sfx2 = new Effect(Effects.Gibs, doorlock, 0, Vector3.zero, Vector3.zero);
                                sfx2.Play();
                                doorlock.Kill(BaseNetworkable.DestroyMode.Gib);
                            }
                            DamageDoneToDoors.Remove(door.net.ID.Value);
                        }
                        else
                        {
                            DamageDoneToDoors[door.net.ID.Value] = curval;
                        }
                    }

                    // Damage toward foundation
                    var parent = door.GetParentEntity() as BuildingBlock;
                    if (parent != null && parent.IsFoundation() && !parent.IsBasementEntity())
                    {
                        // Transfer damage to the foundation the door is sitting upon
                        parent.Hurt(damage / 2f, info.damageTypes.GetMajorityDamageType(), info.Initiator, useProtection: false);
                    }
                    showmsg = false;
                }
                // Handle basement drill attempt
                else if (entity != null && entity is BuildingBlock wall && wall.ShortPrefabName.Contains("wall"))
                {
                    var basement = wall.GetBasement();
                    if (basement != null)
                    {
                        var surfaceBuilding = basement.FirstSurfaceBuilding;
                        var basePlayer = info.InitiatorPlayer;
                        var priv = surfaceBuilding?.GetDominatingBuildingPrivilege();
                        if (basePlayer != null && !(priv?.IsAuthed(basePlayer) ?? false))
                        {
                            // Error not authed or no priv
                            basePlayer?.Notify("cannot drill without auth");
                            showmsg = false;
                            goto End;
                        }

                        var weapon = info.Weapon;
                        if (config.RestrictDrillItems && !config.BasementDrillItems.Contains(weapon?.GetItem()?.info.shortname))
                        {
                            // Error tool required
                            basePlayer?.Notify("help tool required", BasementDrillItems.FirstOrDefault()?.displayName.translated ?? "");
                            var sfx = new Effect(Effects.MetalImpact, entity, 0, info?.HitPositionLocal ?? Vector3.zero, Vector3.one);
                            sfx.Play();
                            showmsg = false;
                            goto End;
                        }

                        var startFoundation = wall.GetLinkedFoundationForWall();
                        var dir = GetDirOfWall(startFoundation, wall);
                        var surfaceFoundation = GetSurfaceFoundation(startFoundation, surfaceBuilding);
                        if (surfaceFoundation != null)
                        {
                            // Error terrain
                            if (IsBlockedByTerrain(surfaceFoundation.transform.position))
                            {
                                basePlayer?.Notify("placement error terrain");
                                var sfx = new Effect(Effects.MetalImpact, entity, 0, info?.HitPositionLocal ?? Vector3.zero, Vector3.one);
                                sfx.Play();
                                showmsg = false;
                                goto End;
                            }
                            var adjSurfaceFoundation = surfaceFoundation.GetLinkedFoundation(dir);
                            if (adjSurfaceFoundation == null)
                            {
                                // Error no foundation
                                basePlayer?.Notify("help no foundation above");
                                var sfx = new Effect(Effects.MetalImpact, entity, 0, info?.HitPositionLocal ?? Vector3.zero, Vector3.one);
                                sfx.Play();
                                showmsg = false;
                                goto End;
                            }
                            if (adjSurfaceFoundation.grade < config.RequiredFoundationGrade)
                            {
                                // Error wrong foundation grade
                                basePlayer?.Notify("help foundation grade", config.RequiredFoundationGrade);
                                var sfx = new Effect(Effects.MetalImpact, entity, 0, info?.HitPositionLocal ?? Vector3.zero, Vector3.one);
                                sfx.Play();
                                showmsg = false;
                                goto End;
                            }
                            var heightDiff = adjSurfaceFoundation.transform.position.y - basement.Elevation;
                            if (heightDiff < 8f)
                            {
                                // Error low elevation
                                basePlayer?.Notify("help bad elevation");
                                var sfx = new Effect(Effects.MetalImpact, entity, 0, info?.HitPositionLocal ?? Vector3.zero, Vector3.one);
                                sfx.Play();
                                showmsg = false;
                                goto End;
                            }
                            var existingBasementFoundationUnderSurface = GetBasementFoundation(adjSurfaceFoundation);
                            if (existingBasementFoundationUnderSurface != null)
                            {
                                // Check if the existing foundation elevation matches this one
                                if (existingBasementFoundationUnderSurface.transform.position.y != basement.Elevation)
                                {
                                    // Error cannot combine mismatching elevations
                                    basePlayer?.Notify("help mismatch elevation");
                                    var sfx = new Effect(Effects.MetalImpact, entity, 0, info?.HitPositionLocal ?? Vector3.zero, Vector3.one);
                                    sfx.Play();
                                    showmsg = false;
                                    goto End;
                                }
                            }

                            // Check to see if there are any basement cells nearby that are NOT the same elevation
                            // as the target elevation.
                            var desiredElevation = startFoundation.transform.position.y;
                            var targetBasementCellPosition = adjSurfaceFoundation.transform.position;
                            targetBasementCellPosition.y = desiredElevation;
                            if (SpherecastFiltered(targetBasementCellPosition, 3.5f, out BaseEntity invalidBasementCell, (entity) =>
                            {
                                return entity != null && entity is BuildingBlock block && block.IsFoundation() && block.IsBasementEntity() && block.transform.position.y != desiredElevation;
                            }))
                            {
                                // Error too close to a mismatching elevation cell
                                basePlayer?.Notify("help mismatch elevation");
                                var sfx = new Effect(Effects.MetalImpact, entity, 0, info?.HitPositionLocal ?? Vector3.zero, Vector3.one);
                                sfx.Play();
                                showmsg = false;
                                goto End;
                            }

                            var triangle = adjSurfaceFoundation.IsTriangle();
                            var isFree = permission.UserHasPermission(basePlayer.UserIDString, PermissionFree);
                            var cost = triangle ? BasementBuildCostTriangle : BasementBuildCostSquare;
                            if (!isFree && cost.Amount > 0)
                            {
                                // Error too poor
                                var invAmt = GetInventoryItemAmount(basePlayer, cost.ItemShortName);
                                if (invAmt < cost.Amount)
                                {
                                    ShowInsufficientResources(basePlayer, cost.ItemDef, invAmt, (int)Math.Ceiling(cost.Amount));
                                    showmsg = false;
                                    goto End;
                                }
                            }

                            var curval = DamageDoneToWalls.GetOrCreate(wall.net.ID.Value);
                            var damage = info.damageTypes.Total();
                            curval += damage;
                            if (curval >= config.DamageRequiredToDrillBasement)
                            {
                                if (!isFree)
                                {
                                    UseInventoryItemAmount(basePlayer, cost.ItemShortName, cost.Amount);
                                }
                                DamageDoneToWalls.Remove(wall.net.ID.Value);

                                // Spawn the basement cell
                                if (adjSurfaceFoundation.IsTriangle())
                                {
                                    Spawn1x1BasementCellTriangle(basement, adjSurfaceFoundation);
                                }
                                else
                                {
                                    Spawn1x1BasementCell(basement, adjSurfaceFoundation);
                                }
                                KillBasementEntity(wall, basement, gibs: true);
                                return false; // RPC error if no return false here
                            }
                            else
                            {
                                DamageDoneToWalls[wall.net.ID.Value] = curval;
                                showmsg = false;
                            }
                        }
                    }
                }
                End:
                var initiatorPlayer = info.InitiatorPlayer;
                info.damageTypes.ScaleAll(0); // Basement entities cannot take damage
                if (initiatorPlayer != null && showmsg)
                {
                    initiatorPlayer?.Notify("cannot modify basement");
                }
            }
            else if (entity is DecayEntity decay && IsBelowTerrain(decay))
            {
                if (info.damageTypes.Has(Rust.DamageType.Decay))
                {
                    info.damageTypes.ScaleAll(0); // prevent decay damage to entities in the basement
                }
            }
            return null;
        }

        bool CanDemolish(BasePlayer basePlayer, BuildingBlock block, BuildingGrade.Enum grade)
        {
            if (basePlayer == null || block == null) { return false; }
            if (allowDestroy) { return true; }
            if (block.IsBasementEntity())
            {
                basePlayer?.Notify(Lang(basePlayer, "cannot modify basement"));
                return false;
            }
            return true;
        }

        object OnStructureRotate(BuildingBlock block, BasePlayer basePlayer)
        {
            if (basePlayer == null || block == null) { return false; }
            if (allowDestroy) { return true; }
            if (block.IsBasementEntity())
            {
                basePlayer?.Notify(Lang(basePlayer, "cannot modify basement"));
                return false;
            }
            return null;
        }

        object OnStructureUpgrade(BaseCombatEntity entity, BasePlayer basePlayer, BuildingGrade.Enum grade)
        {
            if (basePlayer == null || entity == null) { return null; }
            if (allowDestroy) { return null; }
            if (entity.IsBasementEntity())
            {
                basePlayer?.Notify("cannot modify basement");
                return false;
            }
            return null;
        }

        public bool lockCloningInProgress = false;
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null || entity.IsDestroyed) { return; }
            if (entity is DecayEntity decay && decay != null && !decay.IsBasementEntity())  
            {
                Vector3 blockPos = decay.CenterPoint();
                if (Physics.Raycast(blockPos + Vector3.down * 0.1f, Vector3.down, out RaycastHit hitInfo, 1.5f))
                {
                    BaseEntity placedOn = hitInfo.GetEntity();
                    if (placedOn is BuildingBlock placedOnBlock && placedOnBlock.IsBasementEntity() && placedOnBlock.IsCeiling())
                    {
                        NextTick(() =>
                        {
                            // Blocks cannot be placed on top of basement ceilings
                            decay?.Kill(BaseNetworkable.DestroyMode.Gib);
                        });
                        return;
                    }
                }
            }

            // Sync Locks - Spawn Other Lock
            if (lockCloningInProgress == false && config.SyncLocksOnBasementEntrances && entity is BaseLock baseLock && baseLock != null)
            {
                var parent = baseLock.GetParentEntity();
                if (parent != null && parent is Door parentDoor && parentDoor.IsBasementEntrance(out BasementEntrance entrance))
                {
                    var otherEntrance = entrance.ConnectedHatch;
                    if (otherEntrance != null && otherEntrance.BaseEntity != null)
                    {
                        lockCloningInProgress = true;
                        try
                        {
                            SpawnLockOnDoor(otherEntrance.BaseEntity, baseLock);
                        } catch (Exception) { }
                        lockCloningInProgress = false;
                        return;
                    }
                }
            }
            if (entity is StabilityEntity block)
            {
                if (!(block.ShortPrefabName.Contains("floor") || block.ShortPrefabName.Contains("ramp"))) { return; }
                var basement = GetBasementsConnectedToSurfaceBuilding(block.GetBuilding()).FirstOrDefault();
                if (basement == null) { return; }
                NextTick(() =>
                {
                    if (entity == null || entity.IsDestroyed || basement == null) { return; }
                    foreach (var entrance in basement.Entrances)
                    {
                        if (entrance.IsDownHatch == false || entrance.BaseEntity == null) { continue; }
                        DestroyBlocksAbove(entrance.BaseEntity, 1.5f);
                    }
                });
            }
            else if (entity is JunkPile junk)
            {
                if (junk == null) { return; }
                var position = junk.transform.position;
                RaycastHit[] hits = Physics.SphereCastAll(position, 15f, Vector3.down, 15f);

                // Prevent the junkpile from spawning if it would overlap with an existing basement.
                foreach (var hit in hits)
                {
                    if (hit.collider != null)
                    {
                        var obj = hit.collider.gameObject;
                        if ((obj?.ToBaseEntity() is BuildingBlock bblock) == true)
                        {
                            if (bblock.IsBasementEntity())
                            {
                                junk.Kill();
                                return;
                            }
                        }
                    }
                }
            }
        }

        void OnDoorOpened(Door door, BasePlayer basePlayer)
        {
            if (door == null || basePlayer == null || !door.IsBasementEntrance()) { return; }
            var hatch = BasementEntrances[door.net.ID.Value];
            if (hatch == null) { return; }
            var otherHatch = hatch.ConnectedHatch;
            var basement = hatch.Basement;
            if (otherHatch == null || otherHatch.BaseEntity == null || basement == null) { return; }
            float mod = hatch.IsDownHatch ? -2f : 1f;
            basePlayer.Teleport(otherHatch.BaseEntity.transform.position.Mod(y: mod));
            if (basement.ShownTip == false)
            {
                basePlayer.Notify("help basement expand", BasementDrillItems.FirstOrDefault()?.displayName.translated ?? "");
                basement.ShownTip = true;
            }
            NextTick(() =>
            {
                door?.CloseRequest();
            });
        }

        object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if (planner == null || prefab == null) { return null; }
            if (target.entity is BuildingBlock block && block.IsFoundation() && block.IsBasementEntity())
            {
                if (prefab?.deployable?.fullName?.Contains("cupboard.tool") == true)
                {
                    var basePlayer = planner.GetOwnerPlayer();
                    basePlayer?.Notify("placement error tc");
                    return true; // cannot place cupboards in basement
                }
            }
            var prefabFullName = prefab?.fullName;
            var targetPos = target.position;
            if (config.ForbiddenDeployablesInBasement.Contains(prefabFullName) && IsWithinBasement(targetPos))
            {
                var basePlayer = planner.GetOwnerPlayer();
                if (basePlayer != null)
                {
                    basePlayer.SendConsoleCommand("gametip.showtoast", 5, Lang(basePlayer, "toast cannot place in basement"), string.Empty);
                }
                return true; // cannot place in basement
            }
            return null;
        }

        void OnBuildingSplit(BuildingManager.Building oldBuilding, uint newBuildingId)
        {
            if (oldBuilding == null || !oldBuilding.IsBasement()) { return; }
            var oldBasement = oldBuilding.AsBasement();
            var oldBuildingId = oldBuilding.ID;
            NextTick(() =>
            {
                if (oldBasement == null) { return; }
                var newBasement = new BasementStructure
                {
                    BasementBuildingId = newBuildingId,
                    Elevation = oldBasement.Elevation,
                    ShownTip = oldBasement.ShownTip
                };
                if (newBasement.Entrances.Any() == false)
                {
                    // No entrances, then we destroy this basement
                    DestroyBasement(newBasement);
                    NextTick(() =>
                    {
                        // Make sure it is full destroyed
                        if (newBasement != null)
                        {
                            DestroyBasement(newBasement);
                        }
                    });
                    return;
                }

                // This basement is valid, register it
                BasementStructures[newBuildingId] = newBasement;
                newBasement.ClearCache();
                SpawnMissingExteriorWalls(newBasement);
                DestroyBasement(oldBasement);
                NextTick(() =>
                {
                    // Make sure it is full destroyed
                    if (oldBasement != null)
                    {
                        DestroyBasement(oldBasement);
                    }
                });
            });
        }

        void OnBuildingMerge(ServerBuildingManager instance, BuildingManager.Building domBuilding, BuildingManager.Building subBuilding)
        {
            var subBasementEntrances = subBuilding?.AsBasement()?.Entrances.ToArray();
            NextTick(() =>
            {
                var domBasement = domBuilding?.AsBasement(); // This should be the new basement
                if (domBasement == null) { return; }
                var subBasement = subBuilding?.AsBasement();
                domBasement.ClearCache();

                // Destroy entrances if there are too many
                var entranceCount = domBasement.Entrances.Where(x => x.IsDownHatch == false).Count();
                if (entranceCount > config.EntranceLimit)
                {
                    var needToDestroy = entranceCount - config.EntranceLimit;
                    foreach (var entrance in subBasementEntrances)
                    {
                        if (entrance == null || entrance.IsDownHatch) { continue; } // Only target the basement entrances, so it doesnt kill dupes
                        if (needToDestroy <= 0) { break; }
                        DestroyBasementEntrancePair(entrance, gibs: true);
                        needToDestroy--;
                    }
                }

                domBasement?.ClearCache();
                subBasement?.ClearCache();
                if (domBasement?.Entrances.Any() == false)
                {
                    DestroyBasement(domBasement);
                }
                if (subBasement?.Entrances.Any() == false)
                {
                    DestroyBasement(subBasement);
                }
            });
        }

        object OnPlayerViolation(BasePlayer player, AntiHackType type)
        {
            // Prevent violation for players being within terrain (underground)
            if (type == AntiHackType.InsideTerrain) return false;
            return null;
        }
    }
}

namespace Oxide.Plugins
{
    public partial class Basements
    {
        public ItemCost BasementBuildCostSquare;
        public ItemCost BasementBuildCostTriangle;
        public ItemCost BasementEntranceBuildCost;
        public ItemDefinition[] BasementDrillItems;

        public const string TcButtonID = "basementbtn";
        public const string PlacementBannerID = "basementbannner";
        public const string PlacementCostBannerID = "basementbannnercost";
        public const string InventoryCloserID = "basementinventorycloser";

        public HashSet<ulong> PlacementTcs = new HashSet<ulong>();
        public Dictionary<string, BasementBuildSession> PlacementPlayers = new Dictionary<string, BasementBuildSession>();


        // This command is only used by the UI
        [Command("basementui.build"), Permission(PermissionBuild)]
        private void CmdBasementBuild(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null) { return; }
            var sfx1 = new Effect(Effects.LootCopy, basePlayer, 0, Vector3.zero, Vector3.zero);
            sfx1.Play(basePlayer);
            var privid = ulong.Parse(args[0]);
            var priv = FindBaseEntity(privid) as BuildingPrivlidge;
            var session = PlacementPlayers.GetValueOrDefault(basePlayer.UserIDString);
            if (session != null)
            {
                EndPlacement(basePlayer, session.BuildingPrivlidge);
                return;
            };
            CloseInventory(basePlayer);
            if (PlacementTcs.Contains(privid))
            {
                return;
            }
            var building = priv.GetBuilding();
            var foundation = building.buildingBlocks.FirstOrDefault(x => x.grade >= BuildingGrade.Enum.Stone);
            PlacementPlayers[basePlayer.UserIDString] = new BasementBuildSession
            {
                BuildingId = building.ID,
                HitBlock = foundation,
                BuildingPrivlidge = priv
            };
            var sfx2 = new Effect(Effects.LandmineTrigger, basePlayer, 0, Vector3.zero, Vector3.zero);
            sfx2.Play(basePlayer);
            ShowPlacementBanner(basePlayer);
            PlacementTcs.Add(privid);
            ShowToolcupboardButtonForLooters(priv);
        }

        public void CloseInventory(BasePlayer basePlayer)
        {
            basePlayer.EndLooting();
            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Parent = "Under",
                Name = InventoryCloserID,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Autofocus = true,
                        NeedsKeyboard = true,
                        HudMenuInput = false
                    }
                }
            });

            CuiHelper.DestroyUi(basePlayer, InventoryCloserID);
            CuiHelper.AddUi(basePlayer, container);

            timer.In(0.05f, () => // NextTick is too fast
            {
                if (basePlayer == null) { return; }
                CuiHelper.DestroyUi(basePlayer, InventoryCloserID);
            });
        }

        #region Insufficient Resources
        public const string InsufficientResourcesUI = "insufficientresourcesui";

        public void DestroyInsufficientResources(BasePlayer basePlayer)
        {
            CuiHelper.DestroyUi(basePlayer, InsufficientResourcesUI);
        }

        public HashSet<string> OnShowInsufficientResourcesDelay = new HashSet<string>();

        public void ShowInsufficientResources(BasePlayer basePlayer, ItemDefinition itemInfo, int current, int required)
        {
            if (basePlayer == null || !config.InsufficientResourcesUI.Enabled || OnShowInsufficientResourcesDelay.Contains(basePlayer.UserIDString)) { return; }

            var container = new CuiElementContainer();

            var fadein = 0.3f;
            var duration = 3f;
            var fadeout = 0.6f;
            var y = 120f;
            var w = 266f;
            var h = 52f;

            container.Add(new CuiElement
            {   
                Parent = "Hud",
                Name = InsufficientResourcesUI,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"0.5 0",
                        AnchorMax = $"0.5 0",
                        OffsetMin = $"{-(w/2f)} {y}",
                        OffsetMax = $"{(w/2f)} {y+h}"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = InsufficientResourcesUI,
                Name = InsufficientResourcesUI + "bg",
                FadeOut = fadeout,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = fadein,
                        Color = "0.2 0.188 0.184 0.95",
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = InsufficientResourcesUI,
                Name = InsufficientResourcesUI + "txt",
                FadeOut = fadeout,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"0 0",
                        AnchorMax = $"1 1",
                        OffsetMin = $"{7} {0}",
                        OffsetMax = $"{-7} {-4}"
                    },
                    new CuiTextComponent
                    {
                        Text = Lang(basePlayer, "ui insufficient resources"),
                        FadeIn = fadein,
                        FontSize = 15,
                        Align = TextAnchor.UpperLeft,
                        Color = "0.894 0.855 0.82 1"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = InsufficientResourcesUI,
                Name = InsufficientResourcesUI + "cost",
                FadeOut = fadeout,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"0 0",
                        AnchorMax = $"1 0",
                        OffsetMin = $"{6} {6}",
                        OffsetMax = $"{-6} {6+22}"
                    },
                    new CuiImageComponent
                    {
                        Color = "0.545 0.259 0.212 1",
                        FadeIn = fadein,
                    }
                }
            });

            var iconsize = 18;
            container.Add(new CuiElement
            {
                Parent = InsufficientResourcesUI + "cost",
                Name = InsufficientResourcesUI + "icon",
                FadeOut = fadeout,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"0 0.5",
                        AnchorMax = $"0 0.5",
                        OffsetMin = $"{4} {-iconsize/2}",
                        OffsetMax = $"{4+iconsize} {iconsize/2}"
                    },
                    new CuiImageComponent
                    {
                        FadeIn = fadein,
                        ItemId = itemInfo.itemid
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = InsufficientResourcesUI + "cost",
                Name = InsufficientResourcesUI + "itemname",
                FadeOut = fadeout,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"0.15 0",
                        AnchorMax = $"1 1"
                    },
                    new CuiTextComponent
                    {
                        Text = itemInfo.displayName.translated.ToUpper(),
                        FontSize = 12,
                        FadeIn = fadein,
                        Align = TextAnchor.MiddleLeft,
                        Color = "0.894 0.855 0.82 1"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = InsufficientResourcesUI + "cost",
                Name = InsufficientResourcesUI + "cost2",
                FadeOut = fadeout,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"0.59 0",
                        AnchorMax = $"1 1",
                        OffsetMin = $"{0} {2}",
                        OffsetMax = $"{-2} {-2}"
                    },
                    new CuiImageComponent
                    {
                        FadeIn = fadein,
                        Color = "0 0 0 0.6",
                    }
                }
            });

            var ratio = current / (float)required;
            container.Add(new CuiElement
            {
                Parent = InsufficientResourcesUI + "cost2",
                Name = InsufficientResourcesUI + "cost2prog",
                FadeOut = fadeout,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{0} 0",
                        AnchorMax = $"{ratio} 1",
                        OffsetMin = $"{0} {0}",
                        OffsetMax = $"{0} {-0.5f}"
                    },
                    new CuiImageComponent
                    {
                        FadeIn = fadein,
                        Color = "0 0 0 0.6",
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = InsufficientResourcesUI + "cost2",
                Name = InsufficientResourcesUI + "cost2txt",
                FadeOut = fadeout,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"0 0",
                        AnchorMax = $"1 1",
                        OffsetMin = $"{6} {0}",
                        OffsetMax = $"{-6} {0}"
                    },
                    new CuiTextComponent
                    {
                        Text = $"{current}/{required}",
                        FadeIn = fadein,
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "0.894 0.855 0.82 1"
                    }
                }
            });

            CuiHelper.DestroyUi(basePlayer, InsufficientResourcesUI);
            CuiHelper.AddUi(basePlayer, container);
            OnShowInsufficientResourcesDelay.Add(basePlayer.UserIDString);
            timer.In(duration, () =>
            {
                if (basePlayer == null) { return; }
                foreach (var ele in container)
                {
                    if (ele.Name ==  InsufficientResourcesUI) { continue; }
                    // Destroy each so the fadeout triggers
                    CuiHelper.DestroyUi(basePlayer, ele.Name);
                }
            });
            timer.In(duration + fadeout, () =>
            {
                CuiHelper.DestroyUi(basePlayer, InsufficientResourcesUI);
                OnShowInsufficientResourcesDelay.Remove(basePlayer.UserIDString);
            });
        }

        #endregion

        #region Tool Cupboard Button
        public void DestroyToolcupboardButton(BasePlayer basePlayer)
        {
            CuiHelper.DestroyUi(basePlayer, TcButtonID);
        }

        public void ShowToolcupboardButtonForLooters(BuildingPrivlidge priv)
        {
            if (priv == null) { return; }
            foreach (var userid in PlayersLootingTc.GetValueOrDefault(priv.net.ID.Value))
            {
                var target = BasePlayer.Find(userid);
                if (target == null) { continue; }
                ShowToolcupboardButton(target, priv);
            }
        }

        public void ShowToolcupboardButton(BasePlayer basePlayer, BuildingPrivlidge priv)
        {
            if (!config.ToolCupboardUI.Enabled) { return; }
            if (basePlayer == null) { return; }
            if (priv == null)
            {
                DestroyToolcupboardButton(basePlayer);
                return;
            }
            var surfaceBuilding = priv.GetBuilding();
            var basement = GetBasementsConnectedToSurfaceBuilding(surfaceBuilding).FirstOrDefault();
            var showAddEntrance = basement != null;
            var showAddBasement = basement == null;
            var canBuildBasement = basement != null || priv.GetBuilding().buildingBlocks.Any(x => (x is BuildingBlock block) && block.ShortPrefabName.Contains("foundation") && !block.ShortPrefabName.Contains("triangle") && block?.grade >= config.RequiredFoundationGrade);
            var atDoorMax = basement?.Entrances.Where(x => x.IsDownHatch).Count() >= config.EntranceLimit;
            
            if (!showAddEntrance && !showAddBasement)
            {
                DestroyToolcupboardButton(basePlayer);
                return;
            }
            var session = PlacementPlayers.GetValueOrDefault(basePlayer.UserIDString);
            if (session == null && PlacementTcs.Contains(priv.net.ID.Value))
            {
                canBuildBasement = false;
            }
            if (!permission.UserHasPermission(basePlayer.UserIDString, PermissionBuild))
            {
                canBuildBasement = false;
            }
            if (canBuildBasement && IsInPocketDimension(priv))
            {
                canBuildBasement = false;
            }
            var disableBtn = !canBuildBasement || atDoorMax;
            var newbuild = session == null;
            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Parent = "Hud.Menu",
                Name = TcButtonID,
                Components =
                {
                    config.ToolCupboardUI.Position,
                    new CuiButtonComponent
                    {
                        Color = disableBtn ? "0.2 0.2 0.2 1" : newbuild ? "0.361 0.443 0.212 1" : "0.541 0.188 0.133 1",
                        Command = disableBtn ? null : $"basementui.build {priv.net.ID.Value}"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = TcButtonID,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = !newbuild ? Lang(basePlayer, "ui cancel placement") : !canBuildBasement ? Lang(basePlayer, "ui cannot build basement") : atDoorMax ? Lang(basePlayer, "ui at max doors") : showAddEntrance ? Lang(basePlayer, "ui add basement door") : Lang(basePlayer, "ui add a basement"),
                        Align = TextAnchor.MiddleCenter,
                        Color = disableBtn ? "0.5 0.5 0.5 1" : newbuild ? "0.573 0.788 0.192 1" : "0.922 0.78 0.757 1",
                        FontSize = 12
                    }
                }
            });

            CuiHelper.DestroyUi(basePlayer, TcButtonID);
            CuiHelper.AddUi(basePlayer, container);
        }

        #endregion

        #region Placement Banner
        public void DestroyPlacementBanner(BasePlayer basePlayer)
        {
            CuiHelper.DestroyUi(basePlayer, PlacementBannerID);
        }
        
        public void ShowPlacementBanner(BasePlayer basePlayer)
        {
            if (!config.PlacementUI.Enabled) { return; }
            if (basePlayer == null) { return; }
            var session = PlacementPlayers.GetValueOrDefault(basePlayer.UserIDString);
            if (session == null)
            {
                DestroyPlacementBanner(basePlayer);
                return;
            }
            var hasHammer = basePlayer.GetActiveItem()?.info.shortname == "hammer";
            if (hasHammer && !session.HasEquippedHammer)
            {
                session.HasEquippedHammer = true;
            }
            else if (!hasHammer && session.HasEquippedHammer)
            {
                DestroyPlacementBanner(basePlayer);
                EndPlacement(basePlayer, session.BuildingPrivlidge);
                return;
            }

            var p = 8;
            var pa = 6;
            var iconSize = 40;

            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = "Hud",
                Name = PlacementBannerID,
                Components =
                {
                    config.PlacementUI.Position,
                    new CuiImageComponent
                    {
                        Color = "0.2 0.188 0.184 0.9",
                    }
                }
            });

            // Icon
            container.Add(new CuiElement
            {
                Parent = PlacementBannerID,
                Name = PlacementBannerID + "icon",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"0 0.5",
                        AnchorMax = $"0 0.5",
                        OffsetMin = $"{p} {-iconSize/2}",
                        OffsetMax = $"{p+iconSize} {iconSize/2}"
                    },
                    new CuiImageComponent
                    {
                        ItemId = 200773292
                    }
                }
            });

            // Top Text
            container.Add(new CuiElement
            {
                Parent = PlacementBannerID,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"0 0",
                        AnchorMax = $"1 1",
                        OffsetMin = $"{p+iconSize+p+pa} {p}",
                        OffsetMax = $"{-p} {-p}"
                    },
                    new CuiTextComponent
                    {
                        Text = Lang(basePlayer, "ui basement builder"),
                        FontSize = 15,
                        Align = TextAnchor.UpperLeft,
                        Color = "0.894 0.855 0.82 1"
                    }
                }
            });

            // Bottom Text
            container.Add(new CuiElement
            {
                Parent = PlacementBannerID,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"0 0",
                        AnchorMax = $"1 0.6",
                        OffsetMin = $"{p+iconSize+p+pa} {p}",
                        OffsetMax = $"{-p} {-p}"
                    },
                    new CuiTextComponent
                    {
                        Text = Lang(basePlayer, "ui hit a foundation"),
                        FontSize = 11,
                        Align = TextAnchor.MiddleLeft,
                        Color = "0.894 0.855 0.82 0.8"
                    }
                }
            });

            CuiHelper.DestroyUi(basePlayer, PlacementBannerID);
            CuiHelper.AddUi(basePlayer, container);
        }

        #endregion

        #region Placement Helper Methods
        public bool TryPlacement(BasePlayer basePlayer, BasementStructure basement, BuildingBlock block, BasementBuildSession sesion)
        {
            if (basePlayer == null || block == null) { return false; }
            var msg = "";
            var success = false;
            var basementBlock = GetBasementFoundation(block);
            if (!block.PrefabName.Contains("foundation"))
            {
                msg = Lang(basePlayer, "placement error foundation");
            }
            else if (sesion.BuildingId != block.buildingID)
            {
                msg = Lang(basePlayer, "placement error building");
            }
            else if (HasEntranceOnFoundation(basement, block)) 
            {
                msg = Lang(basePlayer, "placement error space");
            }
            else if (block.IsBasementEntity())
            {
                msg = Lang(basePlayer, "placement error in basement");
            }
            else if (((int)block.grade < (int)BuildingGrade.Enum.Stone))
            {
                msg = Lang(basePlayer, "placement error grade", BuildingGrade.Enum.Stone);
            }
            else if (!HasRoomToPlaceEntrance(block))
            {
                msg = Lang(basePlayer, "placement error space");
            }
            else if (basementBlock != null && !HasRoomToPlaceEntrance(basementBlock))
            {
                msg = Lang(basePlayer, "placement error basement space");
            }
            else if (IsBlockedByTerrain(block.transform.position))
            {
                msg = Lang(basePlayer, "placement error terrain");
            }
            else
            {
                success = true;
            }
            basePlayer.Notify(msg);
            return success;
        }

        public void EndPlacement(BasePlayer basePlayer, BuildingPrivlidge priv = null)
        {
            if (basePlayer != null)
            {
                PlacementPlayers.Remove(basePlayer.UserIDString);
                DestroyPlacementBanner(basePlayer);
            }
            if (priv != null)
            {
                ShowToolcupboardButtonForLooters(priv);
                PlacementTcs.Remove(priv.net.ID.Value);
            }
        }

        public readonly string[] InvalidTerrainKeyWords = new string[]
        {
            "train",        // train tunnels
            "cliff",        // cliffs
            "formation",     // rock formations
            "junkpile",      // junk piles and roots,
            "iceberg",       // ice lakes
            "shore"         // ice shores
        };

        public bool IsBlockedByTerrain(Vector3 position, float radius = 2f)
        {
            // Check to see if there are any cliffs, rocks at this location
            RaycastHit[] hits = Physics.SphereCastAll(position + Vector3.up * 5f, radius, Vector3.down, 16f);

            foreach (var hit in hits)
            {
                if (hit.collider != null)
                {
                    //Puts(hit.collider.gameObject.name);
                    string prefabName = hit.collider.gameObject.name.ToLower();
                    if (InvalidTerrainKeyWords.Any(x => prefabName.Contains(x)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool BasementExistsBelow(BasementStructure basement, BuildingBlock block)
        {
            if (basement == null || block == null) { return false; }
            var nearby = GetEntitiesInRadius<BuildingBlock>(block.transform.position.Set(y: basement.Elevation), 0.5f);
            if (nearby.Any() == false)
            {
                Pool<BuildingBlock>.Recycle(nearby);
                return false;
            }
            foreach(var entity in nearby)
            {
                if (entity == null) { continue; }
                if (entity.IsBasementEntity())
                {
                    Pool<BuildingBlock>.Recycle(nearby);
                    return true;
                }
            }
            Pool<BuildingBlock>.Recycle(nearby);
            return false;
        }

        public bool HasRoomToPlaceEntrance(BuildingBlock block)
        {
            if (block == null) { return false; }
            var blockers = GetEntitiesInRadius<BaseEntity>(block.transform.position.Mod(y: 0.5f), 1.3f);
            foreach(var blocker in blockers)
            {
                if (blocker.EqualNetID(block) || (blocker is BasePlayer)) { continue; }
                if (blocker is StorageContainer || blocker is BuildingBlock)
                {
                    return false;
                }
            }
            return true;
        }

        public double GetBasementBuildCost(BasePlayer basePlayer, BuildingBlock startingBlock)
        {
            double cost = 0;
            var foundations = GetFoundationsForBasement(startingBlock, BuildingGrade.Enum.Stone);
            foreach(var foundation in foundations)
            {
                if (foundation.ShortPrefabName.Contains("triangle"))
                {
                    cost += BasementBuildCostTriangle.Amount;
                }
                else
                {
                    cost += BasementBuildCostSquare.Amount;
                }
            }
            return cost;
        }

        public bool HasEntranceOnFoundation(BasementStructure basement, BuildingBlock foundation)
        {
            if (basement == null) { return false; }
            foreach(var entrance in basement.Entrances)
            {
                if (entrance.ParentBuildingBlock.net.ID.Value == foundation.net.ID.Value)
                {
                    return true;
                }
            }
            return false;
        }

        public List<BuildingBlock> GetFoundationsForBasement(BuildingBlock startingFoundation, BuildingGrade.Enum minGrade)
        {
            var validFoundations = new List<BuildingBlock>() { startingFoundation };
            AddConnectedFoundationsRecursive(startingFoundation, minGrade, validFoundations);
            return validFoundations;
        }

        public void AddConnectedFoundationsRecursive(BuildingBlock from, BuildingGrade.Enum minGrade, List<BuildingBlock> list)
        {
            var nearby = GetEntitiesInRadius<BuildingBlock>(from.transform.position, 3f);
            var valid = Pool<BuildingBlock>.Get();
            foreach(var block in nearby)
            {
                if (block != null && block.grade >= minGrade && block.buildingID == from.buildingID && !list.Contains(block))
                {
                    valid.Add(block);
                }
            }
            Pool<BuildingBlock>.Recycle(nearby);
            if (valid.Any() == false)
            {
                Pool<BuildingBlock>.Recycle(valid);
                return;
            }
            foreach (var block in valid)
            {
                list.Add(block);
            }
            foreach(var block in valid)
            {
                AddConnectedFoundationsRecursive(block, minGrade, list);
            }
            Pool<BuildingBlock>.Recycle(valid);
        }
        #endregion
    }
}
 