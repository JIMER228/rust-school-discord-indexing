// Requires: AmusementRides

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Oxide.Plugins.AmusementRides;

namespace Oxide.Plugins
{
    [Info("AmusementRidesMechanicalBull", "Nikedemos", RIDE_VERSION)]
    [Description("Bouncing on this feisty bull, no rodeo ain't dull!")]
    public class AmusementRidesMechanicalBull : AmusementRidesPlugin
    {
        private static AmusementRidesMechanicalBull RideInstance;

        public const string RIDE_VERSION = "1.0.4";

        #region MECHANICAL BULL CONSTANTS
        public const int ITEM_FLARE = 304481038;

        public const int MECHANICAL_BULL_SEGMENT_ROTOR = 1;
        public const int MECHANICAL_BULL_SEGMENT_PULLEY = 2;
        public const int MECHANICAL_BULL_SEGMENT_JACKSHAFT = 3;
        public const int MECHANICAL_BULL_SEGMENT_NECK = 4;
        public const int MECHANICAL_BULL_SEGMENT_RIDER = 5;

        public const int MECHANICAL_BULL_SEGMENT_HORN_LEFT = 5;
        public const int MECHANICAL_BULL_SEGMENT_HORN_RIGHT = 6;

        public const int MECHANICAL_BULL_SEGMENT_EYE_LEFT = 7;
        public const int MECHANICAL_BULL_SEGMENT_EYE_RIGHT = 8;

        public const int MECHANICAL_BULL_SEGMENT_PUPIL_LEFT = 9;
        public const int MECHANICAL_BULL_SEGMENT_PUPIL_RIGHT = 10;

        public const int MECHANICAL_BULL_ELEMENT_MAILBOX = 0;
        public const int MECHANICAL_BULL_ELEMENT_SADDLE = 3;
        public const int MECHANICAL_BULL_ELEMENT_INVISIBLE_CHAIR = 4;

        public const int MECHANICAL_BULL_ELEMENT_BARREL = 5;

        public const int MECHANICAL_BULL_ELEMENT_FOGMACHINE_LEFT = 6;
        public const int MECHANICAL_BULL_ELEMENT_FOGMACHINE_RIGHT = 7;

        public const int MECHANICAL_BULL_GROUP_BODY = 1;
        public const int MECHANICAL_BULL_GROUP_HEAD = 2;

        public const int MECHANICAL_BULL_GROUP_SADDLE = 3;
        public const int MECHANICAL_BULL_GROUP_INVISIBLE_CHAIR = 4;

        public const string PREFAB_SADDLE = "assets/prefabs/vehicle/seats/saddletest.prefab";
        public const string PREFAB_CHAIR_INVISIBLE = "assets/bundled/prefabs/static/chair.invisible.static.prefab";

        #endregion

        [PluginReference]
        private AmusementRides AmusementRides; //has to be private

        public class RideHandlerMechanicalBull : RideHandlerMountBased
        {
            public override object Init()
            {
                name = "MechanicalBull";
                version = RIDE_VERSION;

                return null;
            }

            public override object Prepare(Ride ride, params object[] args)
            {
                //no need for integrity check - this is simple stuff
                UpdatePivots(ride, args);
                //RefreshEntities(ride);
                return base.Prepare(ride, args);
            }

            public override object RideStop(Ride ride, params object[] args)
            {
                base.RideStop(ride, args);

                foreach (var entry in ride.structure.specialGroups[MECHANICAL_BULL_GROUP_HEAD])
                {
                    var foggie = entry.Value as FogMachine;
                    //foggie.CancelInvoke("StartFogging");



                    foggie.SetFlag(BaseEntity.Flags.On, false);
                    foggie.SetFlag(BaseEntity.Flags.Reserved6, false); //emitting off
                    foggie.SetFlag(BaseEntity.Flags.Reserved8, false); //foggin off


                    foggie.CancelInvoke("StartFogging");
                    foggie.CancelInvoke((Action)Delegate.CreateDelegate(typeof(Action), foggie, "StartFogging"));
                    foggie.SendNetworkUpdate();
                }

                ride.delta = 0;
                ride.deltaNormalized = 0;

                UpdatePivots(ride, args);
                //RefreshEntities(ride);

                return null;
            }

            public override object IfMountEntity(Ride ride, BasePlayer player, BaseMountable mountable)
            {
                var result = base.IfMountEntity(ride, player, mountable);
                var toMount = ride.structure.specialGroups[MECHANICAL_BULL_GROUP_SADDLE].FirstOrDefault().Value as BaseMountable;

                if (mountable == toMount) return null;

                if (result == null)
                {

                    RideInstance.NextFrame(() =>
                    {
                        if (toMount != null)
                        {
                            mountable.DismountAllPlayers();
                            toMount.MountPlayer(player);

                            //soft-switch seats
                            if (ride.playerToMountable.ContainsKey(player))
                            {
                                ride.playerToMountable.Remove(player);
                            }

                            if (ride.mountableToPlayer.ContainsKey(mountable))
                            {
                                ride.mountableToPlayer.Remove(mountable);
                            }

                            ride.playerToMountable.Add(player, toMount);
                            ride.mountableToPlayer.Add(toMount, player);
                        }

                    });
                }

                return result;
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

                foreach (var entry in ride.structure.specialGroups[MECHANICAL_BULL_GROUP_HEAD])
                {
                    var foggie = entry.Value as FogMachine;

                    foggie.PostServerLoad();

                    var fuelItem = ItemManager.CreateByItemID(ITEM_LOWGRADE, 500);

                    foggie.inventory.Clear();

                    fuelItem.MoveToContainer(foggie.inventory, 0, true);



                    foggie.SetFlag(BaseEntity.Flags.Reserved7, false); //motion off
                    foggie.SetFlag(BaseEntity.Flags.Reserved6, true); //emitting on
                    foggie.SetFlag(BaseEntity.Flags.Reserved8, true); //foggin on

                    foggie.UpdateMotionMode();

                    foggie.fogLength = 10F;


                    foggie.SetFlag(BaseEntity.Flags.On, true);
                    foggie.CancelInvoke("StartFogging");
                    foggie.CancelInvoke((Action)Delegate.CreateDelegate(typeof(Action), foggie, "StartFogging"));

                    foggie.InvokeRepeating("StartFogging", 0f, foggie.fogLength - 1f);
                    foggie.SendNetworkUpdate();

                }

                return base.RideStartSuccess(ride);
            }

            public override void UpdatePivots(Ride ride, params object[] args)
            {
                if (ride.deltaNormalized == 0)
                {
                    ride.structure.pivots["Rotor"].transform.localEulerAngles = Vector3.zero;
                    ride.structure.pivots["Rotor.Pulley"].transform.localEulerAngles = Vector3.zero;
                    ride.structure.pivots["Rotor.Pulley.Jackshaft"].transform.localPosition = Vector3.zero;
                    ride.structure.pivots["Rotor.Pulley.Jackshaft.Rider"].transform.localPosition = new Vector3(0, 0.42F, 0);
                    ride.structure.pivots["Rotor.Pulley.Jackshaft.Rider"].transform.localEulerAngles = Vector3.zero;

                }
                else
                {
                    var intensity = Mathf.Sin(Mathf.PI * ride.delta / 10F);

                    var intensity2 = Mathf.Cos(ride.delta / 4F);

                    ride.structure.pivots["Rotor"].transform.localEulerAngles = new Vector3(0, ride.deltaNormalized * 720F * intensity, 0F);// ride.deltaNormalized * rotMaxZ * Mathf.Cos(Mathf.PI * ride.delta / 20));

                    ride.structure.pivots["Rotor.Pulley"].transform.localEulerAngles = new Vector3(ride.deltaNormalized * 45 * -Mathf.Sin(Mathf.PI * ride.delta * ride.deltaNormalized) * intensity2, 0F, 0F);

                    ride.structure.pivots["Rotor.Pulley.Jackshaft"].transform.localPosition = new Vector3(0F, 0F, ride.deltaNormalized * 0.5F * Mathf.Sin(Mathf.PI * ride.delta * ride.deltaNormalized) * intensity2);


                    ride.structure.pivots["Rotor.Pulley.Jackshaft.Rider"].transform.localEulerAngles = new Vector3(ride.deltaNormalized * 45 * -Mathf.Cos(Mathf.PI * ride.delta * ride.deltaNormalized) * intensity2 /4F, ride.deltaNormalized * 720F * intensity /20F, 0F);


                    ride.structure.pivots["Rotor.Pulley.Jackshaft.Rider"].transform.localPosition = new Vector3(0F, 0.42F, ride.deltaNormalized * 0.5F * Mathf.Cos(Mathf.PI * ride.delta * ride.deltaNormalized) * intensity2 /2F);
                }
            }

        }

        public override void AddRideHandlers()
        {
            AddRideHandler(new RideHandlerMechanicalBull());
        }

        public override void AddRideDefinitions()
        {
            AmusementInstance.DefinitionRegister(RideInstance, DefaultDefinitionMechanicalBull());
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
        
        #region MECHANICAL BULL DEFINITION

        public RideDefinition DefaultDefinitionMechanicalBull()
        {
            return new RideDefinition
            {
                rideName = "MechanicalBull",
                rideNickname = "Mechanical Bull",
                rideDescription = "Also known as the bucking machine, this mechanical device was invented and popularised by Sherwood Cryer to simulate the sensation or riding a rodeo bull. Hang tight! The beast is so feisty, steam comes out of its nostrils!",
                rideImage = "https://i.imgur.com/ov2cTj5.png",
                rideVersion = RIDE_VERSION,

                handlerName = "MechanicalBull",
                minWaitTime = 1F,
                maxWaitTime = 5F,
                influenceRadius = 100F,
                admissionFeePrice = 10,
                rideItemSkinID = 2442020762,
                rideItemPickupID = ITEM_SMALL_BOX,
                rideCost = new Dictionary<int, int>
                {
                    [ITEM_WOOD] = 1200,
                    [ITEM_FRAGS] = 200,
                },

                workbenchLevel = 1,
                containerType = RideContainerType.SmallBox,

                assignedMusic = new List<string> { "bluegras", "greengras", "brokeback" },


                reversable = true,
                posContainerY = -0.25F,

                posMusicZ = -1F,
                rotMusicY = 180F,

                definitionSegments = new Dictionary<int, RideDefinitionSegment>
                {
                    [MECHANICAL_BULL_SEGMENT_ROTOR] = new RideDefinitionSegment
                    {
                        name = "Rotor",
                        segmentEntities = new Dictionary<int, RideDefinitionSegmentEntity>
                        {
                            [MECHANICAL_BULL_ELEMENT_MAILBOX] = new RideDefinitionSegmentEntity
                            {
                                prefabName = PREFAB_MAILBOX,
                                rotX = 180F,
                                posY = 0.5F,
                                isMobile = true,
                            },
                        },
                        childSegments = new Dictionary<int, RideDefinitionSegment>
                        {
                            [MECHANICAL_BULL_SEGMENT_PULLEY] = new RideDefinitionSegment
                            {
                                name = "Pulley",
                                pivotPosY = 0.75F,
                                childSegments = new Dictionary<int, RideDefinitionSegment>
                                {
                                    [MECHANICAL_BULL_SEGMENT_JACKSHAFT] = new RideDefinitionSegment
                                    {
                                        name = "Jackshaft",
                                        segmentEntities = new Dictionary<int, RideDefinitionSegmentEntity>
                                        {
                                            [MECHANICAL_BULL_ELEMENT_BARREL] = new RideDefinitionSegmentEntity
                                            {
                                                prefabName = PREFAB_OIL_BARREL,
                                                rotX = 90F,
                                                posY = 0.1F,
                                                posZ = -0.7F,

                                            },

                                        },
                                        childSegments = new Dictionary<int, RideDefinitionSegment>
                                        {
                                            [MECHANICAL_BULL_SEGMENT_RIDER] = new RideDefinitionSegment
                                            {
                                                name = "Rider",
                                                pivotPosY = 0.42F,
                                                pivotItemID = 1381010055,
                                                segmentEntities = new Dictionary<int, RideDefinitionSegmentEntity>
                                                {
                                                    [MECHANICAL_BULL_ELEMENT_SADDLE] = new RideDefinitionSegmentEntity
                                                    {
                                                        prefabName = PREFAB_SADDLE,
                                                        specialGroup = MECHANICAL_BULL_GROUP_SADDLE,
                                                        posY = -0.42F,
                                                        isSpecial = true,
                                                    },
                                                    [MECHANICAL_BULL_ELEMENT_INVISIBLE_CHAIR] = new RideDefinitionSegmentEntity
                                                    {
                                                        prefabName = PREFAB_CHAIR_INVISIBLE,
                                                        specialGroup = MECHANICAL_BULL_GROUP_INVISIBLE_CHAIR,
                                                        posY = -0.42F,
                                                        isSpecial = true,
                                                    },
                                                }
                                            },
                                            [MECHANICAL_BULL_SEGMENT_NECK] = new RideDefinitionSegment
                                            {
                                                name = "Neck",
                                                pivotItemID = ITEM_SEWING_KIT,
                                                pivotPosZ = 0.5F,
                                                pivotPosY = 0.5F,
                                                pivotRotX = -30F,

                                                segmentEntities = new Dictionary<int, RideDefinitionSegmentEntity>
                                                {
                                                    [MECHANICAL_BULL_ELEMENT_FOGMACHINE_LEFT] = new RideDefinitionSegmentEntity
                                                    {
                                                        prefabName = PREFAB_FOG_MACHINE,
                                                        locked = true,
                                                        posX = 0.15F,
                                                        posY = -0.1F,
                                                        posZ = 0.2F,

                                                        rotX = 50F,
                                                        rotZ = 90F,

                                                        specialGroup = MECHANICAL_BULL_GROUP_HEAD,
                                                        isSpecial = true
                                                    },
                                                    [MECHANICAL_BULL_ELEMENT_FOGMACHINE_RIGHT] = new RideDefinitionSegmentEntity
                                                    {
                                                        prefabName = PREFAB_FOG_MACHINE,
                                                        locked = true,
                                                        posX = -0.15F,
                                                        posY = -0.12F,
                                                        posZ = 0.19F,

                                                        rotX = 50F,
                                                        rotZ = -90F,

                                                        specialGroup = MECHANICAL_BULL_GROUP_HEAD,
                                                        isSpecial = true
                                                    },
                                                },
                                                childSegments = new Dictionary<int, RideDefinitionSegment>
                                                {
                                                    [MECHANICAL_BULL_SEGMENT_HORN_RIGHT] = new RideDefinitionSegment
                                                    {
                                                        name = "HornRight",
                                                        pivotItemID = ITEM_SICKLE,
                                                        pivotPosZ = 0.35F,
                                                        pivotPosY = 0.20F,
                                                        pivotPosX = 0.35F,

                                                        pivotRotX = -10F,
                                                        pivotRotY = 90F,
                                                        pivotRotZ = -120F,
                                                    },
                                                    [MECHANICAL_BULL_SEGMENT_HORN_LEFT] = new RideDefinitionSegment
                                                    {
                                                        name = "HornLeft",
                                                        pivotItemID = ITEM_SICKLE,
                                                        pivotPosZ = 0.35F,
                                                        pivotPosY = 0.20F,
                                                        pivotPosX = -0.35F,

                                                        pivotRotX = -10F,
                                                        pivotRotY = -90F,
                                                        pivotRotZ = 120F,
                                                    },
                                                    [MECHANICAL_BULL_SEGMENT_EYE_RIGHT] = new RideDefinitionSegment
                                                    {
                                                        name = "EyeRight",
                                                        pivotItemID = ITEM_SNOWBALL,
                                                        pivotPosX = 0.12F,
                                                        pivotPosY = -0.04F,
                                                        pivotPosZ = 0.20F,

                                                        pivotRotY = 180F,
                                                        pivotRotX = 180F,


                                                        childSegments = new Dictionary<int, RideDefinitionSegment>
                                                        {
                                                            [MECHANICAL_BULL_SEGMENT_PUPIL_RIGHT] = new RideDefinitionSegment
                                                            {
                                                                name = "PupilRight",
                                                                pivotPosX = 0.05F,
                                                                pivotPosZ = 0.07F,
                                                                //pivotPosY = -0.05F,
                                                                //pivotPosZ = -0.08F,
                                                                pivotRotY = -90F,

                                                                pivotItemID = ITEM_FLARE
                                                            }
                                                        }
                                                    },
                                                    [MECHANICAL_BULL_SEGMENT_EYE_LEFT] = new RideDefinitionSegment
                                                    {
                                                        name = "EyeLeft",
                                                        pivotItemID = ITEM_SNOWBALL,
                                                        pivotPosX = -0.12F,
                                                        pivotPosY = -0.03F,
                                                        pivotPosZ = 0.20F,


                                                        childSegments = new Dictionary<int, RideDefinitionSegment>
                                                        {
                                                            [MECHANICAL_BULL_SEGMENT_PUPIL_RIGHT] = new RideDefinitionSegment
                                                            {
                                                                name = "PupilLeft",

                                                                pivotPosX = 0.05F,
                                                                pivotPosZ = 0.07F,
                                                                //pivotPosY = -0.05F,
                                                                //pivotPosZ = -0.08F,
                                                                pivotRotY = -90F,
                                                                pivotItemID = ITEM_FLARE
                                                            }
                                                        }
                                                    },
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
