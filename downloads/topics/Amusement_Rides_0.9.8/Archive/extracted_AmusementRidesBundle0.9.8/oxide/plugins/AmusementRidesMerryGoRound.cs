// Requires: AmusementRides

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Oxide.Plugins.AmusementRides;

namespace Oxide.Plugins
{
    [Info("AmusementRidesMerryGoRound", "Nikedemos", RIDE_VERSION)]
    [Description("Round goes the merry... Merry-Go-Round! Spin with our stallions, how does that sound?")]
    public class AmusementRidesMerryGoRound : AmusementRidesPlugin
    {
        private static AmusementRidesMerryGoRound RideInstance;

        public const string RIDE_VERSION = "1.0.5";

        #region MERRY GO ROUND CONSTANTS

        public const int MERRY_MEMORY_SPEED = 1;
        public const int MERRY_MEMORY_HORSE_APPARENT_SPEED = 2;

        public const int MERRY_GROUP_HORSE = 5;
        public const int MERRY_GROUP_TOGGLABLE = 20;

        public const int MERRY_GROUP_ROOF_SIGNS = 30;
        public const int MERRY_GROUP_WALL_SIGNS = 40;

        public const int MERRY_SEGMENT_ROTOR = 1;
        public const int MERRY_SEGMENT_BASE = 2;
        public const int MERRY_SEGMENT_POLE = 3;
        public const int MERRY_SEGMENT_HORSE_RAIL = 4;
        public const int MERRY_SEGMENT_HORSE = 5;
        #endregion


        [PluginReference]
        private AmusementRides AmusementRides; //has to be private

        public class RideHandlerMerryGoRound : RideHandlerMountBased
        {
            public override object Init()
            {
                name = "MerryGoRound";
                version = RIDE_VERSION;

                return null;
            }
            public override object RideStartSuccess(Ride ride)
            {
                foreach (var entity in ride.structure.specialGroups[MERRY_GROUP_TOGGLABLE])
                {
                    if (entity.Value == null) continue;

                    var maybeElectric = entity.Value as IOEntity;

                    if (maybeElectric != null)
                    {
                        maybeElectric.UpdateHasPower(10, 0);
                        entity.Value.SetFlag(BaseEntity.Flags.On, true, false, true);
                    }
                }

                return base.RideStartSuccess(ride);
            }

            public override object RideStop(Ride ride, params object[] args)
            {
                foreach (var entity in ride.structure.specialGroups[MERRY_GROUP_TOGGLABLE])
                {
                    if (entity.Value == null) continue;

                    var maybeElectric = entity.Value as IOEntity;

                    if (maybeElectric != null)
                    {
                        maybeElectric.UpdateFromInput(0, 0);
                        entity.Value.SetFlag(BaseEntity.Flags.On, false, false, true);
                    }

                    entity.Value.SendNetworkUpdateImmediate();
                }

                base.RideStop(ride, args);

                ride.delta = 0;
                ride.deltaNormalized = 0;

                UpdatePivots(ride, args);

                return null;
            }

            public override object Update(Ride ride, params object[] args)
            {
                if (ride.running)
                {
                    ride.deltaNormalized = ride.delta / ride.deltaMax;

                    foreach (var entry in ride.structure.mountablePreviousPosition.ToArray())
                    {
                        ride.structure.mountableCurrentMagnitude[entry.Key] = Vector3.Distance(entry.Key.transform.position, ride.structure.mountablePreviousPosition[entry.Key]) / Time.deltaTime;
                        ride.structure.mountableCurrentVelocity[entry.Key] = (entry.Key.transform.position - ride.structure.mountablePreviousPosition[entry.Key]) / Time.deltaTime;
                        ride.structure.mountablePreviousPosition[entry.Key] = entry.Key.transform.position;
                        ride.structure.mountablePreviousRotation[entry.Key] = entry.Key.transform.eulerAngles;
                        ride.structure.mountableCurrentUp[entry.Key] = entry.Key.transform.up;
                    }

                    speedFactor = 1F;


                    if (ride.deltaNormalized < 0.1F)
                    {
                        speedFactor = ride.deltaNormalized / 0.1F;
                    }


                    UpdatePivots(ride);

                    if (!ride.reversing)
                    {
                        if (ride.delta < ride.deltaMax)
                        {
                            ride.delta += ride.deltaPlus;
                        }
                        else
                        {
                            ride.delta = ride.deltaMax;
                            ride.reversing = true;
                        }
                    }
                    else
                    {
                        if (ride.delta > 0)
                        {
                            ride.delta += ride.deltaMinus;
                        }
                        else
                        {
                            ride.delta = 0;
                            RideStop(ride, args);
                        }
                    }
                }
                return null;
            }

            public float speedFactor;

            public override void UpdatePivots(Ride ride, params object[] args)
            {
                if (ride.structureSpecificMerryGoRound == null)
                {
                    ride.structureSpecificMerryGoRound = new RideStructureMerryGoRoundSpecific
                    {
                        rotorPivot = ride.structure.pivots["Base.Rotor"],
                        polePivots = new DroppedItem[]
                        {
                            ride.structure.pivots["Base.Rotor.Pole0"],
                            ride.structure.pivots["Base.Rotor.Pole1"],
                            ride.structure.pivots["Base.Rotor.Pole2"],
                            ride.structure.pivots["Base.Rotor.Pole3"],
                            ride.structure.pivots["Base.Rotor.Pole4"],
                            ride.structure.pivots["Base.Rotor.Pole5"],
                            ride.structure.pivots["Base.Rotor.Pole6"],
                            ride.structure.pivots["Base.Rotor.Pole7"],
                            ride.structure.pivots["Base.Rotor.Pole8"],
                            ride.structure.pivots["Base.Rotor.Pole9"]
                        }
                    };
                }


                ride.structureSpecificMerryGoRound.rotorPivot.transform.localEulerAngles = ride.structureSpecificMerryGoRound.rotorPivot.transform.localEulerAngles.WithY(ride.structureSpecificMerryGoRound.rotorPivot.transform.localEulerAngles.y + 8F * speedFactor);

                for (var p = 0; p < 10; p++)
                {
                    ride.structureSpecificMerryGoRound.polePivots[p].transform.localPosition = ride.structureSpecificMerryGoRound.polePivots[p].transform.localPosition.WithY(-0.5F + Mathf.Sin((2 * Mathf.PI * ((float)p / 2.5F)) + ride.delta * 2F) / 2F * speedFactor);
                }

            }

            public static readonly Vector3 horseCorrectionPos = new Vector3(0F, 0F, 0F);

            public override object Prepare(Ride ride, params object[] args)
            {
                object result = base.Prepare(ride, args);

                RidableHorse SHADOWFRAXX;
                //SUBJECT TO CHANGE
                RideInstance.timer.Once(3F, () =>
                {
                    int breedIndex = 0;

                    foreach (var horse in ride.structure.specialGroups[MERRY_GROUP_HORSE])
                    {
                        SHADOWFRAXX = horse.Value as RidableHorse;
                        if (SHADOWFRAXX == null) continue;

                        SHADOWFRAXX.SetBreed(breedIndex % 10);
                        SHADOWFRAXX.ApplyBreed(breedIndex % 10);

                        SHADOWFRAXX.walkSpeed = 0;
                        SHADOWFRAXX.runSpeed = 0;
                        SHADOWFRAXX.maxSpeed = 0;
                        SHADOWFRAXX.roadSpeedBonus = 0;
                        SHADOWFRAXX.trotSpeed = 0;
                        SHADOWFRAXX.turnSpeed = 0;

                        SHADOWFRAXX.shouldShowHudHealth = false;

                        SHADOWFRAXX.transform.localPosition = horseCorrectionPos;

                        //ride.handler.IfEntityIsAHorseThenJerkItDownAndThenUpInOrderToMakeItFreezeMidAirInAnAwesomeGallopingPose(SHADOWFRAXX);
                        var pumpkinPie = SHADOWFRAXX.gameObject.AddComponent<HorseHelper>();
                        pumpkinPie.Init(SHADOWFRAXX, ride);
                        breedIndex++;
                    }
                });


                UpdatePivots(ride, args);
                return result;
            }

        }

        public override void AddRideHandlers()
        {
            AddRideHandler(new RideHandlerMerryGoRound());
        }

        public override void AddRideDefinitions()
        {
            AmusementInstance.DefinitionRegister(RideInstance, DefaultDefinitionMerryGoRound());
        }

        void Init()
        {
            InitStuff();
        }

        void Unload()
        {
            UnloadStuff();
        }

        void OnServerInitialized()
        {
            InitializedStuff();
        }

        public override void InitStuff()
        {
            base.InitStuff();
            RideInstance = this;
        }

        public override void UnloadStuff()
        {
            base.UnloadStuff();
            RideInstance = null;
        }
        
        #region MERRY GO ROUND FACTORY
        public static class DefinitionFactoryMerryGoRound
        {
            public static Dictionary<Vector3, Vector3> GetTopping()
            {
                var first = AmusementInstance.TheCircleMethod(25, new Vector3(5F, -6.75F + 6.24F, 0F), Vector3.zero, new Vector3(85F, 90F + 10F, 270F), false, Vector3.zero);
                var second = AmusementInstance.TheCircleMethod(25, new Vector3(5.2F, -6.75F + 9.7F, 0F), Vector3.zero, new Vector3(-85 + 20F, 90F + 10F, 270F), false, Vector3.zero);
                var third = AmusementInstance.TheCircleMethod(10, new Vector3(2.5F, -6.75F + 11F, 0F), Vector3.zero, new Vector3(-85 + 35F, 90F + 20F, 280F), false, Vector3.zero);

                foreach (var b in second)
                {
                    first.Add(b.Key, b.Value);
                }

                foreach (var c in third)
                {
                    first.Add(c.Key, c.Value);
                }

                return first;
            }

            public static Dictionary<Vector3, Vector3> GetToppingLarge()
            {
                return AmusementInstance.TheCircleMethod(25, new Vector3(7.75F, -0.25F, 0F), Vector3.zero, new Vector3(0F, 90, 0F), false, Vector3.zero);
            }

            public static Dictionary<Vector3, Vector3> GetToppingLights()
            {
                return AmusementInstance.TheCircleMethod(25, new Vector3(7.8F, -0.35F, 0F), Vector3.zero, new Vector3(180F, 0F, 0F), false, Vector3.zero);
            }

            public static Dictionary<Vector3, Vector3> GetSignWalls()
            {
                return AmusementInstance.TheCircleMethod(6, new Vector3(2.73F, 2.75F, 1.4F), new Vector3(0F, 30F, 0F), new Vector3(0F, 90F+1.5F, 270F), false, Vector3.zero);
            }

            public static Dictionary<Vector3, Vector3> GetWallXmasLights()
            {
                return AmusementInstance.TheCircleMethod(12, new Vector3(2.73F, 2.75F, 1.4F), new Vector3(0F, 30F, 0F), new Vector3(0F, 90F + 1.5F, 270F), false, Vector3.zero);
            }

            public static Dictionary<Vector3, Vector3> GetRailing()
            {
                var railingStuff = new Dictionary<Vector3, Vector3>();

                var bottoms = AmusementInstance.TheCircleMethod(36, new Vector3(6F, 0F, 0F), Vector3.zero, Vector3.zero, false, Vector3.zero);

                foreach (var b in bottoms)
                {
                    railingStuff.Add(b.Key + Vector3.down * 0.85F, b.Value);
                    //railingStuff.Add(b.Key + Vector3.up * 6.75F, b.Value + Vector3.right * 180F);
                }

                return railingStuff;
            }
        }
        #endregion
        #region MERRY GO ROUND DEFINITION

        public RideDefinition DefaultDefinitionMerryGoRound()
        {
            var result = new RideDefinition
            {
                rideName = "MerryGoRound",
                rideNickname = "Merry-Go-Round",
                rideDescription = "The staple of fairgrounds. Players mount horses and slide up and down long poles (DON'T... EVEN) accompanied by cheesy steam organ tunes.",
                rideImage = "https://i.imgur.com/pTTSBw1.png",

                rideVersion = RIDE_VERSION,
                handlerName = "MerryGoRound",
                reversable = true,

                specialSignData = new Dictionary<int, Dictionary<int, string>>
                {
                    [MERRY_GROUP_ROOF_SIGNS] = new Dictionary<int, string>
                    {
                        [0] = "https://i.imgur.com/qLCCXLh.png",
                    },
                    [MERRY_GROUP_WALL_SIGNS] = new Dictionary<int, string>
                    {
                        [0] = "https://i.imgur.com/qLCCXLh.png"
                    }
                },
                assignedMusic = new List<string> { MERRY_RADIO_URL },

                minWaitTime = 3F,
                influenceRadius = 100F,
                admissionFeePrice = 10,
                hasBuilding = true,

                containerType = RideContainerType.TC,

                posContainerZ = -2F,
                posMusicZ = -2F,
                posMusicY = 1.9F,

                containerHealth = 2000F,

                rideItemSkinID = 2266085524,
                rideItemPickupID = ITEM_TC,

                rideCost = new Dictionary<int, int>
                {
                    [ITEM_WOOD] = 3000,
                    [ITEM_FRAGS] = 1500,
                    [-1130350864] = 10 //RAW HORSE MEAT BECAUSE WHY THE HELL NOT
                },

                workbenchLevel = 2,

                rideVolumes = new List<RideVolumeColliderData>
                {
                    new RideVolumeColliderData
                    {
                        sizeX = 22F,
                        sizeY = 22F,
                        sizeZ = 22F
                    }
                },

                extraData = new ExtraRideData
                {
                    memoryInt = new Dictionary<int, int>
                    {
                        [MERRY_MEMORY_SPEED] = 1,
                        [MERRY_MEMORY_HORSE_APPARENT_SPEED] = 10
                    },
                },
                canJoinLate = false,

                definitionSegments = new Dictionary<int, RideDefinitionSegment>
                {
                    [MERRY_SEGMENT_BASE] = new RideDefinitionSegment
                    {
                        name = "Base",

                        segmentEntities = new Dictionary<int, RideDefinitionSegmentEntity>
                        {
                            #region HARDCODED

                            [0] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = -1.499018F,
                                posY = -0.1000034F,
                                posZ = -7.099002F,
                                rotX = 1.721991E-05F,
                                rotY = 270.0001F,
                                rotZ = 3.175575E-06F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [1] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION,
                                posX = 0.0009849072F,
                                posY = -0.1000032F,
                                posZ = -7.099F,
                                rotX = 1.221096E-05F,
                                rotY = 270.0001F,
                                rotZ = 8.184538E-06F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [2] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,

                                posX = -4.098768F,
                                posY = -0.1000009F,
                                posZ = -5.600417F,
                                rotX = -1.686027E-05F,
                                rotY = 90.00004F,
                                rotZ = -1.833406E-06F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [3] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = -4.300696F,
                                posY = -1.500001F,
                                posZ = -10.44849F,
                                rotX = 2.053346E-05F,
                                rotY = 300.0001F,
                                rotZ = 1.631157E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [5] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = 1.500988F,
                                posY = -0.100003F,
                                posZ = -7.098998F,
                                rotX = -7.202005E-06F,
                                rotY = 90.00007F,
                                rotZ = -1.319349E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [6] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION,
                                posX = 0.0007578135F,
                                posY = -0.09999987F,
                                posZ = -4.099576F,
                                rotX = -5.008957E-06F,
                                rotY = 90.00002F,
                                rotZ = -5.008955E-06F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [8] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = 0.0009770393F,
                                posY = -1.500004F,
                                posZ = -11.599F,
                                rotX = -2.760561E-05F,
                                rotY = 270.0001F,
                                rotZ = 4.77794E-06F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [9] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = -4.098768F,
                                posY = -0.1000009F,
                                posZ = -5.600417F,
                                rotX = 2.186923E-05F,
                                rotY = 270F,
                                rotZ = -3.175551E-06F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [10] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = -2.798766F,
                                posY = -0.1000004F,
                                posZ = -4.850414F,
                                rotX = -5.008953E-06F,
                                rotY = 30.00002F,
                                rotZ = 8.675771E-06F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [11] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = -5.800697F,
                                posY = -3.000002F,
                                posZ = -13.04657F,
                                rotX = 2.052709E-05F,
                                rotY = 300.0001F,
                                rotZ = 1.820242E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [14] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = 2.800994F,
                                posY = -0.1000028F,
                                posZ = -4.848996F,
                                rotX = 1.100044E-05F,
                                rotY = 150.0001F,
                                rotZ = -1.100046E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [15] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = 4.300991F,
                                posY = -1.500004F,
                                posZ = -10.44707F,
                                rotX = -3.212027E-05F,
                                rotY = 240.0001F,
                                rotZ = 1.748476E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [17] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = 0.0007587671F,
                                posY = -0.09999976F,
                                posZ = -2.599575F,
                                rotX = 8.14222E-13F,
                                rotY = 2.892698E-27F,
                                rotZ = 4.07111E-13F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [18] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = 1.500993F,
                                posY = -0.1000028F,
                                posZ = -4.098998F,
                                rotX = -2.193047E-06F,
                                rotY = 90.00007F,
                                rotZ = -8.184536E-06F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [19] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_WALL,
                                posX = 0.0009930134F,
                                posY = -0.1000029F,
                                posZ = -2.599F,
                                rotX = -9.883011E-06F,
                                rotY = 270.0001F,
                                rotZ = -3.120935E-05F,
                                buildingGrade = BuildingGrade.Enum.Metal,
                                damagable = false,
                                isMobile = false,
                            },
                            [21] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = 0.0009732246F,
                                posY = -3.000005F,
                                posZ = -14.599F,
                                rotX = 1.721991E-05F,
                                rotY = 270.0001F,
                                rotZ = 1.319349E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [22] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = -6.898775F,
                                posY = -1.500002F,
                                posZ = -8.948497F,
                                rotX = -1.751891E-05F,
                                rotY = 300F,
                                rotZ = 5.27321E-06F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [23] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION,
                                posX = -3.548285F,
                                posY = -0.1000005F,
                                posZ = -2.050544F,
                                rotX = -3.66681E-06F,
                                rotY = 60.00002F,
                                rotZ = 1.001792E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },

                            [28] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = 5.400516F,
                                posY = -0.1000024F,
                                posZ = -4.848158F,
                                rotX = 1.185136E-05F,
                                rotY = 210F,
                                rotZ = 1.221093E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [29] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = 5.800993F,
                                posY = -3.000004F,
                                posZ = -13.04514F,
                                rotX = 1.784282E-05F,
                                rotY = 240.0001F,
                                rotZ = 1.283386E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [32] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = 2.251833F,
                                posY = -0.100003F,
                                posZ = -1.29948F,
                                rotX = -4.026444E-06F,
                                rotY = 300.0001F,
                                rotZ = -2.324632E-06F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [33] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION,
                                posX = 3.550872F,
                                posY = -0.1000033F,
                                posZ = -2.049478F,
                                rotX = 2.684324E-06F,
                                rotY = 210.0001F,
                                rotZ = 9.035399E-06F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [34] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_WALL,
                                posX = 0.0009930134F,
                                posY = 2.899997F,
                                posZ = -2.599F,
                                rotX = -1.104186E-05F,
                                rotY = 270.0001F,
                                rotZ = -3.087035E-05F,
                                buildingGrade = BuildingGrade.Enum.Metal,
                                damagable = false,
                                isMobile = false,
                            },
                            [35] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FLOOR_TRIANGLE,
                                posX = 0.0009930134F,
                                posY = 2.899997F,
                                posZ = -2.599F,
                                rotX = 3.120934E-05F,
                                rotY = 7.513208E-05F,
                                rotZ = -9.883021E-06F,
                                buildingGrade = BuildingGrade.Enum.Metal,
                                damagable = false,
                                isMobile = false,
                            },
                            [36] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_WALL,
                                posX = -2.249846F,
                                posY = -0.1000031F,
                                posZ = -1.299486F,
                                rotX = 7.781346E-06F,
                                rotY = 330.0001F,
                                rotZ = -1.850721E-05F,
                                buildingGrade = BuildingGrade.Enum.Metal,
                                damagable = false,
                                isMobile = false,
                            },
                            [37] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_WALL,
                                posX = 2.251833F,
                                posY = -0.100003F,
                                posZ = -1.29948F,
                                rotX = -2.631064E-05F,
                                rotY = 210.0001F,
                                rotZ = -1.933702E-05F,
                                buildingGrade = BuildingGrade.Enum.Metal,
                                damagable = false,
                                isMobile = false,
                            },
                            [40] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = -8.398778F,
                                posY = -3.000002F,
                                posZ = -11.54657F,
                                rotX = 2.553604E-05F,
                                rotY = 300.0001F,
                                rotZ = 1.319347E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [41] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = -5.598764F,
                                posY = -0.1000031F,
                                posZ = -0.0006791651F,
                                rotX = -6.351099E-06F,
                                rotY = 90.00007F,
                                rotZ = 1.368471E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [51] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION,
                                posX = 6.148949F,
                                posY = -0.1000039F,
                                posZ = -3.549474F,
                                rotX = 1.404436E-05F,
                                rotY = 120.0001F,
                                rotZ = -7.693284E-06F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [52] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = 6.899074F,
                                posY = -1.500003F,
                                posZ = -8.947065F,
                                rotX = 1.444615E-05F,
                                rotY = 240.0001F,
                                rotZ = 2.039728E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [56] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = 2.249915F,
                                posY = -0.1000026F,
                                posZ = 1.299551F,
                                rotX = -7.201968E-06F,
                                rotY = 239.9999F,
                                rotZ = -3.175494E-06F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [57] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = 4.300874F,
                                posY = -0.1000031F,
                                posZ = -0.7504396F,
                                rotX = 2.324629E-06F,
                                rotY = 30.00004F,
                                rotZ = -1.404436E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [59] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_WALL,
                                posX = -2.249846F,
                                posY = 2.899997F,
                                posZ = -1.299485F,
                                rotX = 8.954437E-06F,
                                rotY = 330.0001F,
                                rotZ = -1.822137E-05F,
                                buildingGrade = BuildingGrade.Enum.Metal,
                                damagable = false,
                                isMobile = false,
                            },
                            [60] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_WALL,
                                posX = 2.251833F,
                                posY = 2.899997F,
                                posZ = -1.299479F,
                                rotX = -2.631065E-05F,
                                rotY = 210.0001F,
                                rotZ = -1.933703E-05F,
                                buildingGrade = BuildingGrade.Enum.Metal,
                                damagable = false,
                                isMobile = false,
                            },
                            [61] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FLOOR_TRIANGLE,
                                posX = -2.249846F,
                                posY = 2.899997F,
                                posZ = -1.299485F,
                                rotX = 1.850721E-05F,
                                rotY = 60.0001F,
                                rotZ = 7.78134E-06F,
                                buildingGrade = BuildingGrade.Enum.Metal,
                                damagable = false,
                                isMobile = false,
                            },
                            [62] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FLOOR_TRIANGLE,
                                posX = 1.501823F,
                                posY = 2.899997F,
                                posZ = -0.0004549474F,
                                rotX = -7.464834E-06F,
                                rotY = 180.0001F,
                                rotZ = -1.215914E-05F,
                                buildingGrade = BuildingGrade.Enum.Metal,
                                damagable = false,
                                isMobile = false,
                            },
                            [63] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_WALL,
                                posX = -2.250676F,
                                posY = -0.1000035F,
                                posZ = 1.300025F,
                                rotX = 7.104507E-06F,
                                rotY = 30.00015F,
                                rotZ = 9.868882E-07F,
                                buildingGrade = BuildingGrade.Enum.Metal,
                                damagable = false,
                                isMobile = false,
                            },
                            [64] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_WALL,
                                posX = 2.251832F,
                                posY = -0.1000034F,
                                posZ = 1.299556F,
                                rotX = -2.781306E-05F,
                                rotY = 150F,
                                rotZ = -1.286842E-06F,
                                buildingGrade = BuildingGrade.Enum.Metal,
                                damagable = false,
                                isMobile = false,
                            },
                            [65] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = -5.598764F,
                                posY = -0.1000031F,
                                posZ = -0.0006791651F,
                                rotX = 1.136005E-05F,
                                rotY = 270.0001F,
                                rotZ = -1.869366E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [66] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION,
                                posX = -3.549716F,
                                posY = -0.1000039F,
                                posZ = 2.050025F,
                                rotX = -1.100044E-05F,
                                rotY = 120.0001F,
                                rotZ = 1.73515E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [75] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = -6.898767F,
                                posY = -0.1000032F,
                                posZ = -0.7506809F,
                                rotX = -5.500214E-06F,
                                rotY = 210.0001F,
                                rotZ = -2.419388E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [77] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = 6.900517F,
                                posY = -0.1000021F,
                                posZ = -2.250079F,
                                rotX = -1.83345E-06F,
                                rotY = 30.00001F,
                                rotZ = -2.222884E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [78] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = 10.04607F,
                                posY = -1.500005F,
                                posZ = -5.799469F,
                                rotX = -3.733322E-05F,
                                rotY = 210.0001F,
                                rotZ = 2.356369E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [79] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = 8.399076F,
                                posY = -3.000004F,
                                posZ = -11.54514F,
                                rotX = 1.444615E-05F,
                                rotY = 240.0001F,
                                rotZ = 2.039728E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [84] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION,
                                posX = 3.548955F,
                                posY = -0.1000028F,
                                posZ = 2.049554F,
                                rotX = 1.83346E-06F,
                                rotY = 150F,
                                rotZ = 1.221092E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [85] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = 2.682209E-05F,
                                posY = -0.1000031F,
                                posZ = 2.597388F,
                                rotX = -8.184463E-06F,
                                rotY = 180F,
                                rotZ = 4.158097E-06F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [86] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = 6.899436F,
                                posY = -0.100005F,
                                posZ = 0.7496914F,
                                rotX = -9.824743E-07F,
                                rotY = 210F,
                                rotZ = 1.636903E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [89] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_WALL,
                                posX = -2.250676F,
                                posY = 2.899997F,
                                posZ = 1.300026F,
                                rotX = 5.514123E-06F,
                                rotY = 30.00015F,
                                rotZ = 6.147106E-06F,
                                buildingGrade = BuildingGrade.Enum.Metal,
                                damagable = false,
                                isMobile = false,
                            },
                            [91] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_WALL,
                                posX = 2.25183F,
                                posY = 2.899997F,
                                posZ = 1.299558F,
                                rotX = -2.663997E-05F,
                                rotY = 150F,
                                rotZ = -1.000996E-06F,
                                buildingGrade = BuildingGrade.Enum.Metal,
                                damagable = false,
                                isMobile = false,
                            },
                            [92] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FLOOR_TRIANGLE,
                                posX = -1.499011F,
                                posY = 2.899997F,
                                posZ = 3.412366E-05F,
                                rotX = 1.599245E-05F,
                                rotY = 0.0001229434F,
                                rotZ = -1.213704E-05F,
                                buildingGrade = BuildingGrade.Enum.Metal,
                                damagable = false,
                                isMobile = false,
                            },
                            [93] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FLOOR_TRIANGLE,
                                posX = 0.7509893F,
                                posY = 2.899997F,
                                posZ = 1.29906F,
                                rotX = -1.426254E-05F,
                                rotY = 120.0001F,
                                rotZ = 3.851661E-07F,
                                buildingGrade = BuildingGrade.Enum.Metal,
                                damagable = false,
                                isMobile = false,
                            },
                            [94] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = -2.250676F,
                                posY = -0.1000035F,
                                posZ = 1.300025F,
                                rotX = -5.991475E-06F,
                                rotY = 120.0001F,
                                rotZ = 1.234254E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [96] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = -6.8978F,
                                posY = -0.1000041F,
                                posZ = 2.250986F,
                                rotX = -1.735151E-05F,
                                rotY = 210.0001F,
                                rotZ = -2.101834E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [97] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION,
                                posX = -6.147797F,
                                posY = -0.1000047F,
                                posZ = 3.550023F,
                                rotX = 1.600939E-05F,
                                rotY = 300.0002F,
                                rotZ = -2.236045E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [98] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = -2.799494F,
                                posY = -0.1000034F,
                                posZ = 4.848217F,
                                rotX = -1.368467E-05F,
                                rotY = 150F,
                                rotZ = 1.27022E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [106] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = -11.19734F,
                                posY = -1.500003F,
                                posZ = -1.499852F,
                                rotX = 2.579248E-05F,
                                rotY = 0.0001263585F,
                                rotZ = 2.216224E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [108] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = 11.20004F,
                                posY = -1.500004F,
                                posZ = -1.499238F,
                                rotX = -3.703492E-05F,
                                rotY = 180F,
                                rotZ = 3.297351E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [109] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = 12.64414F,
                                posY = -3.000007F,
                                posZ = -7.299481F,
                                rotX = -3.733323E-05F,
                                rotY = 210.0001F,
                                rotZ = 2.356369E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [113] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = 2.799433F,
                                posY = -0.1000033F,
                                posZ = 4.849426F,
                                rotX = -1.65006E-05F,
                                rotY = 210F,
                                rotZ = 5.859876E-06F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [114] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION,
                                posX = 6.147034F,
                                posY = -0.1000035F,
                                posZ = 3.549559F,
                                rotX = 1.721988E-05F,
                                rotY = 59.99996F,
                                rotZ = -6.842418E-06F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [115] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION,
                                posX = 2.777576E-05F,
                                posY = -0.1000035F,
                                posZ = 4.097389F,
                                rotX = 9.16705E-06F,
                                rotY = 90.00002F,
                                rotZ = 1.319342E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [116] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = 6.898956F,
                                posY = -0.1000049F,
                                posZ = 2.250526F,
                                rotX = 1.551816E-05F,
                                rotY = 150F,
                                rotZ = 1.587772E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [119] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_WALL,
                                posX = 0.0009874105F,
                                posY = 2.899996F,
                                posZ = 2.599056F,
                                rotX = -9.644299E-06F,
                                rotY = 90.00008F,
                                rotZ = 1.176125E-05F,
                                buildingGrade = BuildingGrade.Enum.Metal,
                                damagable = false,
                                isMobile = false,
                            },
                            [122] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FLOOR_TRIANGLE,
                                posX = 0.0009874105F,
                                posY = 2.899996F,
                                posZ = 2.599056F,
                                rotX = -7.464839E-06F,
                                rotY = 180.0001F,
                                rotZ = -1.215914E-05F,
                                buildingGrade = BuildingGrade.Enum.Metal,
                                damagable = false,
                                isMobile = false,
                            },
                            [123] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = -11.19733F,
                                posY = -1.500005F,
                                posZ = 1.500146F,
                                rotX = -2.144989E-06F,
                                rotY = 0.0001161132F,
                                rotZ = 1.371226E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [124] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = -5.397792F,
                                posY = -0.1000053F,
                                posZ = 4.84906F,
                                rotX = 2.736942E-05F,
                                rotY = 30.00013F,
                                rotZ = 1.100043E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [125] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = -10.04492F,
                                posY = -1.500006F,
                                posZ = 5.80002F,
                                rotX = -2.607863E-06F,
                                rotY = 30.00014F,
                                rotZ = 2.565593E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [126] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = -2.799011F,
                                posY = -0.100004F,
                                posZ = 6.349051F,
                                rotX = -2.468513E-05F,
                                rotY = 210F,
                                rotZ = -7.333589E-06F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [136] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = -14.19734F,
                                posY = -3.000005F,
                                posZ = -1.499844F,
                                rotX = 2.579248E-05F,
                                rotY = 0.0001229434F,
                                rotZ = 2.216224E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [138] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = 14.20004F,
                                posY = -3.000006F,
                                posZ = -1.499247F,
                                rotX = -3.703492E-05F,
                                rotY = 180F,
                                rotZ = 3.297351E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [141] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = 4.099435F,
                                posY = -0.1000039F,
                                posZ = 5.599429F,
                                rotX = -2.016746E-05F,
                                rotY = 270F,
                                rotZ = -1.319342E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [142] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = 4.099435F,
                                posY = -0.1000039F,
                                posZ = 5.599429F,
                                rotX = 2.517641E-05F,
                                rotY = 90F,
                                rotZ = 8.184456E-06F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [143] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = 10.04415F,
                                posY = -1.500005F,
                                posZ = 5.799568F,
                                rotX = -2.697352E-05F,
                                rotY = 150F,
                                rotZ = 2.849386E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [144] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION,
                                posX = 3.004074E-05F,
                                posY = -0.1000042F,
                                posZ = 7.097391F,
                                rotX = 1.820238E-05F,
                                rotY = 1.366038E-05F,
                                rotZ = -1.4176E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [145] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = 11.20004F,
                                posY = -1.500003F,
                                posZ = 1.500764F,
                                rotX = 1.556998E-05F,
                                rotY = 180F,
                                rotZ = 2.800686E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [152] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = -14.19733F,
                                posY = -3.000005F,
                                posZ = 1.500154F,
                                rotX = 3.054497E-05F,
                                rotY = 0.0001195283F,
                                rotZ = 1.453562E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [153] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = -6.897093F,
                                posY = -1.500005F,
                                posZ = 8.947119F,
                                rotX = 2.392105E-05F,
                                rotY = 60.00008F,
                                rotZ = 2.41018E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [154] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = -12.64299F,
                                posY = -3.000008F,
                                posZ = 7.300028F,
                                rotX = -2.607864E-06F,
                                rotY = 30.00014F,
                                rotZ = 2.565593E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [155] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = -1.499008F,
                                posY = -0.1000047F,
                                posZ = 7.099053F,
                                rotX = -1.283384E-05F,
                                rotY = 270.0001F,
                                rotZ = -2.687815E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [167] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = 2.799434F,
                                posY = -0.1000036F,
                                posZ = 6.349427F,
                                rotX = 6.351052E-06F,
                                rotY = 330F,
                                rotZ = -2.90712E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [168] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = 6.899439F,
                                posY = -1.500006F,
                                posZ = 8.94751F,
                                rotX = -1.727971E-05F,
                                rotY = 120F,
                                rotZ = 4.163569E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [169] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = 12.64223F,
                                posY = -3.000007F,
                                posZ = 7.299574F,
                                rotX = -2.697352E-05F,
                                rotY = 150F,
                                rotZ = 2.849386E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [171] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = 3.290176E-05F,
                                posY = -1.500006F,
                                posZ = 11.59739F,
                                rotX = -6.1748E-06F,
                                rotY = 90.00001F,
                                rotZ = 3.179038E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [172] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = 14.20005F,
                                posY = -3.000005F,
                                posZ = 1.500769F,
                                rotX = 1.270224E-05F,
                                rotY = 180F,
                                rotZ = 2.835193E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [177] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = -8.397095F,
                                posY = -3.000006F,
                                posZ = 11.54519F,
                                rotX = 2.455351E-05F,
                                rotY = 60.00007F,
                                rotZ = 2.687815E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [178] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = -4.29901F,
                                posY = -1.500006F,
                                posZ = 10.44712F,
                                rotX = -2.716086E-06F,
                                rotY = 60.00008F,
                                rotZ = 3.367853E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [186] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = 4.298955F,
                                posY = -1.500006F,
                                posZ = 10.44834F,
                                rotX = -2.600977E-05F,
                                rotY = 120F,
                                rotZ = 3.329472E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [187] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = 8.39944F,
                                posY = -3.000007F,
                                posZ = 11.54559F,
                                rotX = 2.272014E-05F,
                                rotY = 120F,
                                rotZ = 2.468511E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [189] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = 3.528595E-05F,
                                posY = -3.000007F,
                                posZ = 14.59739F,
                                rotX = 1.918496E-05F,
                                rotY = 90.00002F,
                                rotZ = 2.321134E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [191] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = -5.799012F,
                                posY = -3.000007F,
                                posZ = 13.04519F,
                                rotX = 2.956247E-05F,
                                rotY = 60.00007F,
                                rotZ = 2.186919E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [193] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = 5.798957F,
                                posY = -3.000007F,
                                posZ = 13.04642F,
                                rotX = -2.600977E-05F,
                                rotY = 120F,
                                rotZ = 3.329472E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [194] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION_TRIANGLE,
                                posX = -2.249245F,
                                posY = -0.1000005F,
                                posZ = -1.300541F,
                                rotX = 1.342149E-06F,
                                rotY = 60F,
                                rotZ = 5.008962E-06F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [195] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_DOOR_METAL_SINGLE,
                                posX = 0.0009874105F,
                                posY = -0.1000039F,
                                posZ = 2.599056F,
                                rotX = -9.644299E-06F,
                                rotY = 90.00008F,
                                rotZ = 1.176125E-05F,
                                buildingGrade = BuildingGrade.Enum.Twigs,
                                damagable = false,
                                isMobile = false,
                            },
                            [199] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_DOORWAY,
                                posX = 0.0009874105F,
                                posY = -0.1000039F,
                                posZ = 2.599056F,
                                rotX = -9.644299E-06F,
                                rotY = 90.00008F,
                                rotZ = 1.176125E-05F,
                                buildingGrade = BuildingGrade.Enum.Metal,
                                damagable = false,
                                isMobile = false,
                            },
                            [196] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_FOUNDATION,
                                posX = -6.146364F,
                                posY = -0.1000009F,
                                posZ = -3.55055F,
                                rotX = 8.675767E-06F,
                                rotY = 240F,
                                rotZ = -1.502688E-05F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [197] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = -12.64156F,
                                posY = -3.000001F,
                                posZ = -7.300563F,
                                rotX = -9.718781E-06F,
                                rotY = 330F,
                                rotZ = -5.269215E-07F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            [198] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_STEPS,
                                posX = -10.04348F,
                                posY = -1.500001F,
                                posZ = -5.800557F,
                                rotX = -9.718781E-06F,
                                rotY = 330F,
                                rotZ = -5.269215E-07F,
                                buildingGrade = BuildingGrade.Enum.Wood,
                                damagable = false,
                                isMobile = false,
                            },
                            #endregion

                        }
                    }
                }
            };

            //add topping

            var counter = 0;

            var railingStuff = DefinitionFactoryMerryGoRound.GetRailing();

            foreach (var entry in railingStuff)
            {
                result.definitionSegments[MERRY_SEGMENT_BASE].segmentEntities.Add(300 + counter, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_SHOTGUN_TRAP,
                    locked = true,

                    posX = entry.Key.x,
                    posY = entry.Key.y,
                    posZ = entry.Key.z,

                    rotX = entry.Value.x,
                    rotY = entry.Value.y,
                    rotZ = entry.Value.z,

                    damagable = false,
                    isMobile = false,
                });

                counter++;
            }

            result.definitionSegments[MERRY_SEGMENT_BASE].childSegments = new Dictionary<int, RideDefinitionSegment>
            {
                [MERRY_SEGMENT_ROTOR] = new RideDefinitionSegment
                {
                    name = "Rotor",
                    pivotPosY = 4.4F,

                    childSegments = new Dictionary<int, RideDefinitionSegment>
                    {
                        [MERRY_SEGMENT_POLE] = new RideDefinitionSegment
                        {
                            name = "Pole",
                            pivotItemID = ITEM_SPRING,

                            cloneCount = 10,
                            clonePosY = 0F,
                            clonePosZ = 6F,
                            /*
                            segmentEntities = new Dictionary<int, RideDefinitionSegmentEntity>
                            {
                                
                                [0] = new RideDefinitionSegmentEntity
                                {
                                    propItemID = ITEM_PITCHFORK,
                                    posY = -6.75F/2
                                },
                            },*/
                            childSegments = new Dictionary<int, RideDefinitionSegment>
                            {
                                [MERRY_SEGMENT_HORSE_RAIL] = new RideDefinitionSegment
                                {
                                    name = "HorseRail",
                                    pivotPosY = -3.75F,
                                    pivotRotY = 90F,
                                    pivotItemID = ITEM_SPRING,

                                    childSegments = new Dictionary<int, RideDefinitionSegment>
                                    {
                                        [MERRY_SEGMENT_HORSE] = new RideDefinitionSegment
                                        {
                                            name = "Horse",

                                            pivotPosY = 0.4F,
                                            pivotItemID = ITEM_SPRING,

                                            segmentEntities = new Dictionary<int, RideDefinitionSegmentEntity>
                                            {
                                                [0] = new RideDefinitionSegmentEntity
                                                {
                                                    prefabName = PREFAB_HORSE,
                                                    posY = 0F,
                                                    posZ = -0.3F,

                                                    isSpecial = true,
                                                    specialGroup = MERRY_GROUP_HORSE
                                                },

                                                [1] = new RideDefinitionSegmentEntity
                                                {
                                                    propItemID = ITEM_PITCHFORK,
                                                    rotX = -90F,

                                                    rotZ = -90F,
                                                    posY = 0.5F,
                                                    //posZ = 0.3F,
                                                },

                                                [2] = new RideDefinitionSegmentEntity
                                                {
                                                    propItemID = ITEM_PITCHFORK,
                                                    rotX = 90F,

                                                    rotZ = -90F,
                                                    posY = -1.25F,
                                                    //posZ = 0.3F,
                                                },

                                                [3] = new RideDefinitionSegmentEntity
                                                {
                                                    propItemID = ITEM_PITCHFORK,
                                                    rotX = 90F,

                                                    rotZ = -90F,
                                                    posY = 2F,
                                                    //posZ = 0.3F,
                                                },

                                                [4] = new RideDefinitionSegmentEntity
                                                {
                                                    propItemID = ITEM_PITCHFORK,
                                                    rotX = -90F,

                                                    rotZ = -90F,
                                                    posY = 3.5F,
                                                    //posZ = 0.3F,
                                                },

                                                [5] = new RideDefinitionSegmentEntity
                                                {
                                                    prefabName = PREFAB_SPEAKER,
                                                    posY = 0.5F,
						    rotY = 180F
                                                }
                                            }
                                        },
                                    },
                                }
                            }
                        }
                    }
                }
            };

            var toppingStuff = DefinitionFactoryMerryGoRound.GetTopping();


            foreach (var entry in toppingStuff)
            {
                result.definitionSegments[MERRY_SEGMENT_BASE].childSegments[MERRY_SEGMENT_ROTOR].segmentEntities.Add(1000 + counter, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_SIGN_WOODEN_HUGE,
                    locked = true,
                    isSpecial = true,

                    specialGroup = -1,

                    posX = entry.Key.x,
                    posY = entry.Key.y,
                    posZ = entry.Key.z,

                    rotX = entry.Value.x,
                    rotY = entry.Value.y,
                    rotZ = entry.Value.z,

                    damagable = false,
                    isMobile = true,
                });

                counter++;
            }

            var toppingStuff2 = DefinitionFactoryMerryGoRound.GetToppingLarge();

            var specialCounter = 0;

            int thisSpecialGroup;

            int maxElements = toppingStuff2.Count;

            foreach (var entry in toppingStuff2)
            {
                thisSpecialGroup = -1;

                if (specialCounter == maxElements - 1)
                {
                    thisSpecialGroup = MERRY_GROUP_ROOF_SIGNS;
                }

                result.definitionSegments[MERRY_SEGMENT_BASE].childSegments[MERRY_SEGMENT_ROTOR].segmentEntities.Add(1000 + counter, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_SIGN_WOODEN_HUGE,
                    locked = true,
                    isSpecial = true,

                    specialGroup = thisSpecialGroup,

                    posX = entry.Key.x,
                    posY = entry.Key.y,
                    posZ = entry.Key.z,

                    rotX = entry.Value.x,
                    rotY = entry.Value.y,
                    rotZ = entry.Value.z,

                    damagable = false,
                    isMobile = true,
                });

                counter++;
                specialCounter++;
            }

            var toppingStuff3 = DefinitionFactoryMerryGoRound.GetToppingLights();

            foreach (var entry in toppingStuff3)
            {
                result.definitionSegments[MERRY_SEGMENT_BASE].childSegments[MERRY_SEGMENT_ROTOR].segmentEntities.Add(2000 + counter, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_SIRENLIGHT,
                    locked = true,
                    isSpecial = true,

                    specialGroup = MERRY_GROUP_TOGGLABLE,

                    posX = entry.Key.x,
                    posY = entry.Key.y,
                    posZ = entry.Key.z,

                    rotX = entry.Value.x,
                    rotY = entry.Value.y,
                    rotZ = entry.Value.z,

                    damagable = false,
                    isMobile = true,
                });

                counter++;
            }

            var signStuff = DefinitionFactoryMerryGoRound.GetSignWalls();

            var signCounter = 0;
            foreach (var entry in signStuff)
            {
                float correction = signCounter == 4 ? 2.15F : 0F;

                result.definitionSegments[MERRY_SEGMENT_BASE].segmentEntities.Add(8000 + counter, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_PICTUREFRAME_XXL,
                    locked = true,
                    isSpecial = true,

                    specialGroup = MERRY_GROUP_WALL_SIGNS,

                    posX = entry.Key.x,
                    posY = entry.Key.y + correction,
                    posZ = entry.Key.z,

                    rotX = entry.Value.x,
                    rotY = entry.Value.y,
                    rotZ = entry.Value.z,

                    damagable = false,
                    isMobile = false,
                });

                signCounter++;
                counter++;
            }

            var xmasLightsStuff = signStuff;

            foreach (var entry in xmasLightsStuff)
            {
                result.definitionSegments[MERRY_SEGMENT_BASE].segmentEntities.Add(9000 + counter, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_XMAS_LIGHTS,

                    posX = entry.Key.x,
                    posY = entry.Key.y-0.75F,
                    posZ = entry.Key.z,

                    rotX = entry.Value.x,
                    rotY = entry.Value.y,
                    rotZ = entry.Value.z,

                    damagable = false,
                    isMobile = false,
                });

                counter++;
            }

            xmasLightsStuff = AmusementInstance.TheCircleMethod(18, new Vector3(8.1F, 1.9F, 0F), Vector3.zero, new Vector3(90F, 0, 90F), false, Vector3.zero);

            foreach (var entry in xmasLightsStuff.ToDictionary(e => e.Key, e => e.Value))
            {
                //duplicate
                xmasLightsStuff.Add(new Vector3(entry.Key.x, entry.Key.y - 2.1F, entry.Key.z), entry.Value);
            }

            var bottomLightStuff1 = AmusementInstance.TheCircleMethod(25, new Vector3(4F, -0.65F, 0F), Vector3.zero, new Vector3(70F, -30F, 10F), false, Vector3.zero);
            var bottomLightStuff2 = AmusementInstance.TheCircleMethod(25, new Vector3(6.1F, -0.45F, 0F), new Vector3(0, -7F, 0F), new Vector3(70F, -40F, 10F), false, Vector3.zero);
            var bottomLightStuff3 = AmusementInstance.TheCircleMethod(25, new Vector3(7.5F, -0.35F, 0F), new Vector3(0, -12F, 0F), new Vector3(70F, -60F, 10F), false, Vector3.zero);

            var topLightsVert = AmusementInstance.TheCircleMethod(25, new Vector3(8.3F, 0.8F, 0F), new Vector3(0, 0F, 0F), new Vector3(-40F, 180F, 270F), false, Vector3.zero);

            var topLightStuff1 = AmusementInstance.TheCircleMethod(25, new Vector3(4.3F, 2.25F + 1.25F, 0F), new Vector3(0, 5F, 0F), new Vector3(130F, -18F, 30F), false, Vector3.zero);
            var topLightStuff2 = AmusementInstance.TheCircleMethod(25, new Vector3(6.1F, 2.25F + 0.38F, 0F), new Vector3(0, -5F, 0F), new Vector3(130F, -40F, 22F), false, Vector3.zero);
            var topLightStuff3 = AmusementInstance.TheCircleMethod(25, new Vector3(7.3F, 2.1F, 0F), new Vector3(0, -12F, 0F), new Vector3(130F, -60F, 15F), false, Vector3.zero);

            var smallestCircle = AmusementInstance.TheCircleMethod(8, new Vector3(3.2F, 4F, 0F), Vector3.zero, new Vector3(90F, 0F, 270F), false, Vector3.zero);

            xmasLightsStuff = xmasLightsStuff.Concat(bottomLightStuff1).Concat(bottomLightStuff2).Concat(bottomLightStuff3).Concat(topLightsVert).Concat(topLightStuff1).Concat(topLightStuff2).Concat(topLightStuff3).Concat(smallestCircle).ToDictionary(e => e.Key, e => e.Value);

            foreach (var entry in xmasLightsStuff)
            {

                    result.definitionSegments[MERRY_SEGMENT_BASE].childSegments[MERRY_SEGMENT_ROTOR].segmentEntities.Add(10000 + counter, new RideDefinitionSegmentEntity
                    {
                        prefabName = PREFAB_XMAS_LIGHTS,

                        posX = entry.Key.x,
                        posY = entry.Key.y,
                        posZ = entry.Key.z,

                        rotX = entry.Value.x,
                        rotY = entry.Value.y,
                        rotZ = entry.Value.z,

                        damagable = false,
                        isMobile = true,
                    });

                    counter++;
            }

            return result;
        }
        #endregion
    }
}
