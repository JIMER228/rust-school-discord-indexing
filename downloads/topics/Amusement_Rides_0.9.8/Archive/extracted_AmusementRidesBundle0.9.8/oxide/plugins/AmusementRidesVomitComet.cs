// Requires: AmusementRides

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Oxide.Plugins.AmusementRides;

namespace Oxide.Plugins
{
    [Info("AmusementRidesVomitComet", "Nikedemos", RIDE_VERSION)]
    [Description("Prepare your stomach, prepare your back, hop on the Comet, let's make bones crack!")]
    public class AmusementRidesVomitComet : AmusementRidesPlugin
    {
        private static AmusementRidesVomitComet RideInstance;

        public const string RIDE_VERSION = "1.0.3";

        [PluginReference]
        private AmusementRides AmusementRides; //has to be private
        
        #region VOMIT COMET CONSTANTS

        public const int VOMIT_SEGMENT_SLIDER = 0;

        public const int VOMIT_GROUP_TOGGLABLE = 0;

        public const int VOMIT_SEGMENT_WOBBLER = 1;
        public const int VOMIT_SEGMENT_ROTOR = 2;
        public const int VOMIT_SEGMENT_ARM = 3;
        public const int VOMIT_SEGMENT_CHAIR = 4;

        public const int VOMIT_ENTITY_REFINERY = 0;
        public const int VOMIT_ENTITY_SIRENLIGHT = 1;
        public const int VOMIT_ENTITY_FIREWORK = 2;
        public const int VOMIT_ENTITY_LADDER = 3;
        public const int VOMIT_ENTITY_MAILBOX = 4;
        public const int VOMIT_ENTITY_CHAIR = 5;
        public const int VOMIT_ENTITY_SPEAKER = 6;

        public const int VOMIT_GROUP_CHAIR = 1;
        #endregion

        public class RideHandlerVomitComet : RideHandlerMountBased
        {
            public Vector3 slidingVector = new Vector3(0, 2.25F, 0);
            public float rotMaxX = 30F;
            public float rotMaxY = 0F;
            public float rotMaxZ = 30F;

            public override object Init()
            {
                name = "VomitComet";
                version = RIDE_VERSION;

                return null;
            }

            public override object Prepare(Ride ride, params object[] args)
            {
                if (PivotIntegrityCheck(ride))
                {
                    UpdatePivots(ride, args);

                    return base.Prepare(ride, args);
                }
                else
                {
                    return false;
                }
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

            public override object RideStartSuccess(Ride ride)
            {
                foreach (var entity in ride.structure.specialGroups[VOMIT_GROUP_TOGGLABLE])
                {
                    if (entity.Value == null) continue;
                    switch (entity.Value.PrefabName)
                    {
                        case PREFAB_SIRENLIGHT:
                            {
                                var maybeElectric = entity.Value as IOEntity;

                                if (maybeElectric != null)
                                {
                                    maybeElectric.UpdateHasPower(1, 0);
                                    entity.Value.SetFlag(BaseEntity.Flags.On, true, false, true);
                                }
                            }
                            break;

                        case PREFAB_REFINERY:
                            {
                                entity.Value.SetFlag(BaseEntity.Flags.On, true, false, true);
                            }
                            break;

                        case PREFAB_FIREWORK:
                            {
                                entity.Value.SetFlag(BaseEntity.Flags.On, true, false, true);
                                entity.Value.SetFlag(BaseEntity.Flags.OnFire, true, false, true);
                            }
                            break;
                    }
                    entity.Value.SendNetworkUpdateImmediate();
                }

                return base.RideStartSuccess(ride);
            }

            public override object RideStop(Ride ride, params object[] args)
            {
                foreach (var entity in ride.structure.specialGroups[VOMIT_GROUP_TOGGLABLE])
                {
                    if (entity.Value == null) continue;
                    switch (entity.Value.PrefabName)
                    {
                        case PREFAB_SIRENLIGHT:
                            {
                                var maybeElectric = entity.Value as IOEntity;

                                if (maybeElectric != null)
                                {
                                    maybeElectric.UpdateFromInput(0, 0);
                                    entity.Value.SetFlag(BaseEntity.Flags.On, false, false, true);
                                }
                            }
                            break;

                        case PREFAB_REFINERY:
                            {
                                entity.Value.SetFlag(BaseEntity.Flags.On, false, false, true);
                            }
                            break;

                        case PREFAB_FIREWORK:
                            {
                                entity.Value.SetFlag(BaseEntity.Flags.On, false, false, true);
                                entity.Value.SetFlag(BaseEntity.Flags.OnFire, false, false, true);
                            }
                            break;
                    }

                    entity.Value.SendNetworkUpdateImmediate();

                }

                base.RideStop(ride, args);

                ride.delta = 0;
                ride.deltaNormalized = 0;

                UpdatePivots(ride, args);
                //RefreshEntities(ride);

                return null;
            }

            public override void UpdatePivots(Ride ride, params object[] args)
            {
                ride.structure.pivots["Slider"].transform.localPosition = new Vector3(0, ride.definition.definitionSegments[VOMIT_SEGMENT_SLIDER].pivotPosY + ride.deltaNormalized * slidingVector.y, 0);

                if (ride.deltaNormalized == 0)
                {
                    ride.structure.pivots["Slider.Wobbler"].transform.localEulerAngles = Vector3.zero;
                }
                else
                {
                    ride.structure.pivots["Slider.Wobbler"].transform.localEulerAngles = new Vector3(ride.deltaNormalized * rotMaxX * Mathf.Sin(ride.delta), 0F, ride.deltaNormalized * rotMaxZ * Mathf.Cos(Mathf.PI * ride.delta / 20));
                }

                ride.structure.pivots["Slider.Wobbler.Rotor"].transform.localEulerAngles = ride.structure.pivots["Slider.Wobbler.Rotor"].transform.localEulerAngles + new Vector3(0F, ride.delta/2F, 0F);
            }

            public virtual bool PivotIntegrityCheck(Ride ride)
            {
                if (ride == null) return false;
                if (ride.structure == null) return false;
                if (ride.structure.pivots == null) return false;

                if (ride.structure.pivots.ContainsKey("Slider") && ride.structure.pivots.ContainsKey("Slider.Wobbler") && ride.structure.pivots.ContainsKey("Slider.Wobbler.Rotor"))
                {
                    return true;
                }
                else
                {
                    if (RideInstance != null)
                    {
                        RideInstance.PrintError($"ERROR: Pivot integrity check for {ride.name} ({ride.rideData.nickname}) trying to apply handler {name} failed. Make sure the pivots are named correctly. For clones, don't forget about appending their number.");
                    }
                    return false;
                }
            }
        }

        public override void AddRideHandlers()
        {
            AddRideHandler(new RideHandlerVomitComet());
        }

        public override void AddRideDefinitions()
        {
            AmusementInstance.DefinitionRegister(RideInstance, DefaultDefinitionVomitComet());
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

        #region VOMIT COMET DEFINITION

        public RideDefinition DefaultDefinitionVomitComet()
        {
            return new RideDefinition
            {
                rideName = "VomitComet",
                rideNickname = "The Vomit Comet",
                rideDescription = "The ride with pyrotechnics, rainbow-coloured chairs, funky German techno and a very real possibility of launching someone into the sunset.",
                rideImage = "https://i.imgur.com/Xa5DjPf.png",

                handlerName = "VomitComet",
                rideVersion = RIDE_VERSION,
                minWaitTime = 3F,
                influenceRadius = 100F,
                admissionFeePrice = 10,
                rideItemSkinID = 2262052378,
                rideItemPickupID = ITEM_REFINERY,
                rideCost = new Dictionary<int, int>
                {
                    [ITEM_WOOD] = 2000,
                    [ITEM_FRAGS] = 500,
                },
                workbenchLevel = 1,

                assignedMusic = new List<string> { "denseintenseloop16", "slowspyk", "psyloop", "mutkanto", "babys1stsynthwave" },

                posMusicZ = 1F,
                
                //and on the opposite side...
                posContainerZ = -1F,
                rotContainerY = 180F,


                reversable = true,

                specialGroupSkins = new Dictionary<int, Dictionary<int, ulong>>
                {
                    [VOMIT_GROUP_CHAIR] = new Dictionary<int, ulong>
                    {
                        [0] = SKIN_CHAIR_RED,
                        [1] = SKIN_CHAIR_YELLOW,
                        [2] = SKIN_CHAIR_GREEN,
                        [3] = SKIN_CHAIR_CYAN,
                        [4] = SKIN_CHAIR_BLUE,
                        [5] = SKIN_CHAIR_MAGENTA
                    }
                },

                definitionSegments = new Dictionary<int, RideDefinitionSegment>
                {
                    [VOMIT_SEGMENT_SLIDER] = new RideDefinitionSegment
                    {
                        name = "Slider",
                        pivotPosY = 0.25F,
                        pivotItemID = 95950017, //pipe

                        segmentEntities = new Dictionary<int, RideDefinitionSegmentEntity>
                        {
                            [VOMIT_ENTITY_REFINERY] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_REFINERY,
                                isSpecial = true,
                                specialGroup = VOMIT_GROUP_TOGGLABLE,
                                posY = -2.25F,
                                locked = true
                            }
                        },

                        childSegments = new Dictionary<int, RideDefinitionSegment>
                        {
                            [VOMIT_SEGMENT_WOBBLER] = new RideDefinitionSegment
                            {
                                name = "Wobbler",
                                pivotPosY = 0.15F,// 0.25F,
                                pivotItemID = -363689972, //snowball
                                segmentEntities = new Dictionary<int, RideDefinitionSegmentEntity>
                                {
                                    [VOMIT_ENTITY_SIRENLIGHT] = new RideDefinitionSegmentEntity
                                    {
                                        prefabName = PREFAB_SIRENLIGHT,
                                        isSpecial = true,
                                        specialGroup = VOMIT_GROUP_TOGGLABLE,
                                    },

                                    [VOMIT_ENTITY_FIREWORK] = new RideDefinitionSegmentEntity
                                    {
                                        prefabName = PREFAB_FIREWORK,
                                        isSpecial = true,
                                        specialGroup = VOMIT_GROUP_TOGGLABLE,
                                        posY = 0.5F
                                    }
                                },

                                childSegments = new Dictionary<int, RideDefinitionSegment>
                                {
                                    [VOMIT_SEGMENT_ROTOR] = new RideDefinitionSegment
                                    {
                                        name = "Rotor",
                                        pivotPosY = 0.5F,
                                        pivotItemID = 963906841, //rock

                                        childSegments = new Dictionary<int, RideDefinitionSegment>
                                        {
                                            [VOMIT_SEGMENT_ARM] = new RideDefinitionSegment
                                            {
                                                name = "Arm",
                                                cloneCount = 6,
                                                clonePosX = -0.5F,
                                                cloneRotZ = 90F,

                                                //pivotItemID = 1491189398,

                                                segmentEntities = new Dictionary<int, RideDefinitionSegmentEntity>
                                                {
                                                    [VOMIT_ENTITY_LADDER] = new RideDefinitionSegmentEntity
                                                    {
                                                        prefabName = PREFAB_LADDER,
                                                        posY = 1.55F,
                                                        rotY = 90F
                                                    },

                                                    [VOMIT_ENTITY_MAILBOX] = new RideDefinitionSegmentEntity
                                                    {
                                                        prefabName = PREFAB_MAILBOX,
                                                        posY = -1.35F,
                                                        locked = true
                                                    },
                                                },
                                                childSegments = new Dictionary<int, RideDefinitionSegment>
                                                {
                                                    [VOMIT_SEGMENT_CHAIR] = new RideDefinitionSegment
                                                    {
                                                        name = "Chair",
                                                        pivotItemID = 1401987718, //duct tape

                                                        pivotPosX = 0F,
                                                        pivotPosY = 3F,
                                                        pivotRotZ = -90F,

                                                        pivotRotY = 180F,

                                                        segmentEntities = new Dictionary<int, RideDefinitionSegmentEntity>
                                                        {
                                                            [VOMIT_ENTITY_CHAIR] = new RideDefinitionSegmentEntity
                                                            {
                                                                rotX = 180F,
                                                                posY = 0.4F,

                                                                prefabName = PREFAB_CHAIR,
                                                                isSpecial = true,
                                                                specialGroup = VOMIT_GROUP_CHAIR
                                                            },
                                                            [VOMIT_ENTITY_SPEAKER] = new RideDefinitionSegmentEntity
                                                            {
                                                                prefabName = PREFAB_SPEAKER,
                                                                posY = 0.1F,
                                                                rotY = 180F
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }
        #endregion
    }
}
