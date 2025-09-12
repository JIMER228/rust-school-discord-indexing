// Requires: AmusementRides

using System.Collections.Generic;
using UnityEngine;
using static Oxide.Plugins.AmusementRides;

namespace Oxide.Plugins
{
    [Info("AmusementRidesFerrisWheel", "Nikedemos", RIDE_VERSION)]
    [Description("Hop on glorious Ferris Wheel, gonna see some sights for real!")]
    public class AmusementRidesFerrisWheel : AmusementRidesPlugin
    {
        private static AmusementRidesFerrisWheel RideInstance;

        public const string RIDE_VERSION = "1.1.0";

        [PluginReference]
        private AmusementRides AmusementRides; //has to be private

        #region FERRIS WHEEL CONSTANTS
        
        public const int FERRIS_GROUP_XMAS_LIGHT = 2512;

        public const int FERRIS_GROUP_BUTTON_START_STOP = 1;
        public const int FERRIS_GROUP_BUTTON_LIGHTS_ON_OFF = 2;
        public const int FERRIS_GROUP_BUTTON_FASTER = 3;
        public const int FERRIS_GROUP_BUTTON_SLOWER = 4;

        public const int FERRIS_MEMORY_LIGHTS_ON = 0;
        public const int FERRIS_MEMORY_SPEED = 1;
        
        public const int FERRIS_SEGMENT_PILLAR_A = 0;
        public const int FERRIS_SEGMENT_PILLAR_B = 1;
        public const int FERRIS_SEGMENT_ROTOR_SOCKET = 2;
        public const int FERRIS_SEGMENT_ROTOR_PLUG = 3;
        public const int FERRIS_SEGMENT_SPOKE = 4;
        public const int FERRIS_SEGMENT_RIM = 500;
        public const int FERRIS_SEGMENT_GONDOLA = 7;
        public const int FERRIS_SEGMENT_WALL = 8;

        public const int FERRIS_ENTITY_SHOTGUN_TRAP = 0;
        public const int FERRIS_ENTITY_SPEAKER = 1;
	    public const int FERRIS_ENTITY_SPEAKER_BARREL = 2;

        public const int FERRIS_ENTITY_FLOOR_LEFT = 3;
        public const int FERRIS_ENTITY_FLOOR_RIGHT = 4;

        public const int FERRIS_ENTITY_ROOF_LEFT = 5;
        public const int FERRIS_ENTITY_ROOF_RIGHT = 6;

        public const int FERRIS_ENTITY_PILLAR_BOTTOM_LEFT_BACK = 7;
        public const int FERRIS_ENTITY_PILLAR_BOTTOM_RIGHT_BACK = 8;
        public const int FERRIS_ENTITY_PILLAR_BOTTOM_LEFT_FRONT = 9;
        public const int FERRIS_ENTITY_PILLAR_BOTTOM_RIGHT_FRONT = 10;

        public const int FERRIS_ENTITY_PILLAR_TOP_LEFT_BACK = 11;
        public const int FERRIS_ENTITY_PILLAR_TOP_RIGHT_BACK = 12;
        public const int FERRIS_ENTITY_PILLAR_TOP_LEFT_FRONT = 13;
        public const int FERRIS_ENTITY_PILLAR_TOP_RIGHT_FRONT = 14;

        public const int FERRIS_ENTITY_XMAS_BOTTOM_LEFT = 15;
        public const int FERRIS_ENTITY_XMAS_BOTTOM_RIGHT = 16;
        public const int FERRIS_ENTITY_XMAS_BOTTOM_BACK = 17;
        public const int FERRIS_ENTITY_XMAS_BOTTOM_FRONT = 18;

        public const int FERRIS_ENTITY_XMAS_TOP_LEFT = 19;
        public const int FERRIS_ENTITY_XMAS_TOP_CENTER = 20;
        public const int FERRIS_ENTITY_XMAS_TOP_RIGHT = 21;

        public const int FERRIS_ENTITY_WALL_SIGN = 22;

        public const int FERRIS_ENTITY_XMAS_LIGHTS_1A = 800;

        public const int FERRIS_ENTITY_HOBO_BARREL_1 = 101;
        public const int FERRIS_ENTITY_HOBO_BARREL_2 = 102;
        public const int FERRIS_ENTITY_HOBO_BARREL_3 = 103;
        public const int FERRIS_ENTITY_HOBO_BARREL_4 = 104;

        public const int FERRIS_ENTITY_OIL_BARREL_1 = 201;
        public const int FERRIS_ENTITY_OIL_BARREL_2 = 202;

        public const int FERRIS_ENTITY_LADDER_1A = 312;
        public const int FERRIS_ENTITY_LADDER_1B = 313;

        public const int FERRIS_ENTITY_LADDER_2A = 314;
        public const int FERRIS_ENTITY_LADDER_2B = 315;

        public const int FERRIS_ENTITY_LADDER_3A = 316;
        public const int FERRIS_ENTITY_LADDER_3B = 317;

        public const int FERRIS_ENTITY_LADDER_4A = 318;
        public const int FERRIS_ENTITY_LADDER_4B = 319;

        public const int FERRIS_ENTITY_LADDER_5A = 320;
        public const int FERRIS_ENTITY_LADDER_5B = 321;

        public const int FERRIS_ENTITY_LADDER_6A = 322;
        public const int FERRIS_ENTITY_LADDER_6B = 323;

        public const int FERRIS_ENTITY_LADDER_7A = 324;
        public const int FERRIS_ENTITY_LADDER_7B = 325;

        public const int FERRIS_ENTITY_LADDER_8A = 326;
        public const int FERRIS_ENTITY_LADDER_8B = 327;

        public const int FERRIS_GROUP_XXL_SIGNS = 666;

        public const int FERRIS_GROUP_GONDOLA_SIGNS = 667;

        public const int FERRIS_GROUP_OTHER_SIGNS = 668;
        #endregion

        public class RideHandlerFerrisWheel : RideHandlerColliderBased
        {            
            public static readonly string[] gondolaPivotNames = new string[]
            {
                "Base.RotorSocket.RotorPlug.Gondola0",
                "Base.RotorSocket.RotorPlug.Gondola1",
                "Base.RotorSocket.RotorPlug.Gondola2",
                "Base.RotorSocket.RotorPlug.Gondola3",
                "Base.RotorSocket.RotorPlug.Gondola4",
                "Base.RotorSocket.RotorPlug.Gondola5",
                "Base.RotorSocket.RotorPlug.Gondola6",
                "Base.RotorSocket.RotorPlug.Gondola7",
                "Base.RotorSocket.RotorPlug.Gondola8",
                "Base.RotorSocket.RotorPlug.Gondola9",
                "Base.RotorSocket.RotorPlug.Gondola10",
                "Base.RotorSocket.RotorPlug.Gondola11",
                "Base.RotorSocket.RotorPlug.Gondola12",
                "Base.RotorSocket.RotorPlug.Gondola13",
                "Base.RotorSocket.RotorPlug.Gondola14",
                "Base.RotorSocket.RotorPlug.Gondola15",

            };

            public override object Init()
            {
                name = "FerrisWheel";
                version = RIDE_VERSION;

                return null;
            }

            public void LightsOn(Ride ride)
            {
                foreach (var entry in ride.structure.specialGroups[FERRIS_GROUP_XMAS_LIGHT])
                {
                    entry.Value.limitNetworking = false;
                }

                ride.rideData.extraData.memoryBool[FERRIS_MEMORY_LIGHTS_ON] = true;
            }

            public void LightsOff(Ride ride)
            {
                foreach (var entry in ride.structure.specialGroups[FERRIS_GROUP_XMAS_LIGHT])
                {
                    entry.Value.limitNetworking = true;
                }

                ride.rideData.extraData.memoryBool[FERRIS_MEMORY_LIGHTS_ON] = false;
            }

            public override object Update(Ride ride, params object[] args)
            {
                if (ride.running)
                {
                    ride.deltaNormalized = ride.delta / ride.deltaMax;

                    ride.delta += (float)ride.rideData.extraData.memoryInt[FERRIS_MEMORY_SPEED]/(float)20F;

                    if (ride.delta >= 360F)
                        ride.delta -= 360F;
                    if (ride.delta < 0)
                        ride.delta += 360F;

                    UpdatePivots(ride);
                }
                return null;
            }

            //these need to be moved to data as "welcome" and "goodbye" messages.
            //Ride messages can support various variables like: player name, ride name, admission fee.


            public override object WhenButtonPress(Ride ride, PressButton button, BasePlayer player)
            {
                if (base.WhenButtonPress(ride, button, player) == null)
                {
                    if (ride.structure.entityToSpecialGroup.ContainsKey(button.net.ID))
                    {
                        var specialButtonGroupID = ride.structure.entityToSpecialGroup[button.net.ID];

                        var unpressTime = 1F;

                        switch (specialButtonGroupID)
                        {
                            case FERRIS_GROUP_BUTTON_START_STOP:
                                {
                                    if (!ride.running)
                                    {
                                        RideStartSuccess(ride);
                                    }
                                    else
                                    {
                                        RideStop(ride);
                                    }
                                }
                                break;
                            case FERRIS_GROUP_BUTTON_LIGHTS_ON_OFF:
                                {
                                    if (!ride.rideData.extraData.memoryBool[FERRIS_MEMORY_LIGHTS_ON])
                                    {
                                        LightsOn(ride);
                                    }
                                    else
                                    {
                                        LightsOff(ride);
                                    }
                                }
                                break;
                            case FERRIS_GROUP_BUTTON_FASTER:
                                {
                                    SpeedUp(ride);
                                    unpressTime = 0.2F;
                                }
                                break;
                            case FERRIS_GROUP_BUTTON_SLOWER:
                                {
                                    SlowDown(ride);
                                    unpressTime = 0.2F;
                                }
                                break;
                        }

                        button.Invoke(() =>
                        {
                            button.Unpress();
                        }, unpressTime);

                        return null;
                    }
                }

                return null;
            }

            public void SpeedUp(Ride ride)
            {
                if (ride.rideData.extraData.memoryInt[FERRIS_MEMORY_SPEED] == -1 )
                {
                    ride.rideData.extraData.memoryInt[FERRIS_MEMORY_SPEED] = 1;
                }
                else
                {
                    if (ride.rideData.extraData.memoryInt[FERRIS_MEMORY_SPEED] < 20)
                    {
                        ride.rideData.extraData.memoryInt[FERRIS_MEMORY_SPEED] += 1;
                    }
                }
            }

            public void SlowDown(Ride ride)
            {
                if (ride.rideData.extraData.memoryInt[FERRIS_MEMORY_SPEED] == 1)
                {
                    ride.rideData.extraData.memoryInt[FERRIS_MEMORY_SPEED] = -1;
                }
                else
                {
                    if (ride.rideData.extraData.memoryInt[FERRIS_MEMORY_SPEED] > -20)
                    {
                        ride.rideData.extraData.memoryInt[FERRIS_MEMORY_SPEED] -= 1;
                    }
                }
            }

            //update runtimes, just for the Ferris Wheel
            internal Vector3 counterRotation;
            internal float angle = 360F / (float)gondolaPivotNames.Length;
            internal bool conditionB;
            internal float currentAngle;
            internal bool conditionA;

            internal float factor;

            internal float activationAngle = 30F;
            internal float deadAngle = 20F;

            internal const string FORMAT_STRING_CONCAT = "{0}{1}";

            public override void UpdatePivots(Ride ride, params object[] args)
            {
                if (ride.structureSpecificFerrisWheel == null)
                {
                    ride.structureSpecificFerrisWheel = new RideStructureFerrisWheelSpecific
                    {
                        rotorPivot = ride.structure.pivots["Base.RotorSocket.RotorPlug"],
                        gondolaPivotArray = new DroppedItem[]
                        {
                            ride.structure.pivots[gondolaPivotNames[0]],
                            ride.structure.pivots[gondolaPivotNames[1]],
                            ride.structure.pivots[gondolaPivotNames[2]],
                            ride.structure.pivots[gondolaPivotNames[3]],
                            ride.structure.pivots[gondolaPivotNames[4]],
                            ride.structure.pivots[gondolaPivotNames[5]],
                            ride.structure.pivots[gondolaPivotNames[6]],
                            ride.structure.pivots[gondolaPivotNames[7]],
                            ride.structure.pivots[gondolaPivotNames[8]],
                            ride.structure.pivots[gondolaPivotNames[9]],
                            ride.structure.pivots[gondolaPivotNames[10]],
                            ride.structure.pivots[gondolaPivotNames[11]],
                            ride.structure.pivots[gondolaPivotNames[12]],
                            ride.structure.pivots[gondolaPivotNames[13]],
                            ride.structure.pivots[gondolaPivotNames[14]],
                            ride.structure.pivots[gondolaPivotNames[15]],
                        },

                        gondolaWallPivotArray = new DroppedItem[][]
                        {
                            new DroppedItem[4]
                            {
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[0], ".Wall0")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[0], ".Wall1")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[0], ".Wall2")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[0], ".Wall3")]
                            },

                            new DroppedItem[4]
                            {
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[1], ".Wall0")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[1], ".Wall1")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[1], ".Wall2")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[1], ".Wall3")]
                            },

                            new DroppedItem[4]
                            {
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[2], ".Wall0")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[2], ".Wall1")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[2], ".Wall2")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[2], ".Wall3")]
                            },
                            new DroppedItem[4]
                            {
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[3], ".Wall0")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[3], ".Wall1")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[3], ".Wall2")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[3], ".Wall3")]
                            },

                            new DroppedItem[4]
                            {
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[4], ".Wall0")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[4], ".Wall1")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[4], ".Wall2")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[4], ".Wall3")]
                            },
                            new DroppedItem[4]
                            {
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[5], ".Wall0")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[5], ".Wall1")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[5], ".Wall2")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[5], ".Wall3")]
                            },
                            new DroppedItem[4]
                            {
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[6], ".Wall0")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[6], ".Wall1")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[6], ".Wall2")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[6], ".Wall3")]
                            },
                            new DroppedItem[4]
                            {
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[7], ".Wall0")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[7], ".Wall1")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[7], ".Wall2")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[7], ".Wall3")]
                            },
                            new DroppedItem[4]
                            {
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[8], ".Wall0")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[8], ".Wall1")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[8], ".Wall2")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[8], ".Wall3")]
                            },
                            new DroppedItem[4]
                            {
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[9], ".Wall0")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[9], ".Wall1")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[9], ".Wall2")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[9], ".Wall3")]
                            },
                            new DroppedItem[4]
                            {
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[10], ".Wall0")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[10], ".Wall1")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[10], ".Wall2")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[10], ".Wall3")]
                            },
                            new DroppedItem[4]
                            {
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[11], ".Wall0")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[11], ".Wall1")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[11], ".Wall2")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[11], ".Wall3")]
                            },
                            new DroppedItem[4]
                            {
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[12], ".Wall0")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[12], ".Wall1")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[12], ".Wall2")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[12], ".Wall3")]
                            },
                            new DroppedItem[4]
                            {
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[13], ".Wall0")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[13], ".Wall1")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[13], ".Wall2")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[13], ".Wall3")]
                            },
                            new DroppedItem[4]
                            {
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[14], ".Wall0")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[14], ".Wall1")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[14], ".Wall2")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[14], ".Wall3")]
                            },
                            new DroppedItem[4]
                            {
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[15], ".Wall0")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[15], ".Wall1")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[15], ".Wall2")],
                                ride.structure.pivots[string.Format(FORMAT_STRING_CONCAT, gondolaPivotNames[15], ".Wall3")]
                            },
                        }
                    };
                }

                ride.structureSpecificFerrisWheel.rotorPivot.transform.localEulerAngles = new Vector3(0, ride.delta, 0);

                counterRotation = new Vector3(-90, 0, -ride.delta);

                factor  = 0;

                for (var g = 0; g < ride.structureSpecificFerrisWheel.gondolaPivotArray.Length; g++)
                {
                    //currentGondolaPivot = gondolaPivots[g];

                    ride.structureSpecificFerrisWheel.gondolaPivotArray[g].transform.localEulerAngles = counterRotation;
                    currentAngle = 90F + ride.delta + g * angle;

                    if (currentAngle >= 360F)
                    {
                        currentAngle -= 360F;
                    }
                    else
                    {
                        if (currentAngle<0F)
                        {
                            currentAngle += 360F;
                        }
                    }

                    //between 0 and activation angle
                    conditionA = (currentAngle >= 0F && currentAngle <= (activationAngle));

                    //between -activation angle and 0 (360)
                    conditionB = (currentAngle >= (360F  -activationAngle) && currentAngle <= 360F);

                    factor = 0F;

                    if (conditionA || conditionB)
                    {
                        if (conditionA)
                        {
                            if (currentAngle<=deadAngle)
                            {
                                factor = 1;
                            }
                            else
                            {
                                factor = 1 - ((currentAngle-deadAngle)/(activationAngle - deadAngle));
                            }
                        }
                        else
                        {
                            if (currentAngle >= (360F - activationAngle) && currentAngle <= (360F - activationAngle +  (activationAngle - deadAngle)))
                            {
                                factor = (currentAngle - (360F - activationAngle)) / (activationAngle - deadAngle);
                            }
                            else
                            {
                                factor = 1;
                            }
                        }

                        ride.structureSpecificFerrisWheel.gondolaWallPivotArray[g][0].transform.localEulerAngles = new Vector3(factor * 100F, 0F, 90F);
                        ride.structureSpecificFerrisWheel.gondolaWallPivotArray[g][1].transform.localEulerAngles = new Vector3(factor * 100F, 90F, 90F);
                        ride.structureSpecificFerrisWheel.gondolaWallPivotArray[g][2].transform.localEulerAngles = new Vector3(factor * 100F, 180F, 90F);
                        ride.structureSpecificFerrisWheel.gondolaWallPivotArray[g][3].transform.localEulerAngles = new Vector3(factor * 100F, 270F, 90F);
                    }
                }
            }

            public override object Prepare(Ride ride, params object[] args)
            {
                var result = base.Prepare(ride, args);


                UpdatePivots(ride, args);

                RideInstance.timer.Once(3F, () =>
                {
                    if (!ride.rideData.extraData.memoryBool[FERRIS_MEMORY_LIGHTS_ON])
                    {
                        foreach (var entry in ride.structure.specialGroups[FERRIS_GROUP_XMAS_LIGHT])
                        {
                            entry.Value.limitNetworking = true;
                        }
                    }
                });

                return result;
            }

        }

        public override void AddRideHandlers()
        {
            AddRideHandler(new RideHandlerFerrisWheel());
        }

        public override void AddRideDefinitions()
        {
            AmusementInstance.DefinitionRegister(RideInstance, DefaultDefinitionFerrisWheel());
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

        #region FERRIS WHEEL FACTORY
        public static class DefinitionFactoryFerrisWheel
        {
            internal static uint sidesX = 15;
            internal static uint sidesZ = 4;

            internal static float armLength = 32F;

            internal static int armCount = 16;
            internal static int rimCount = 2;

            internal static float ladderPosX = 1.6F;
            internal static float ladderRotY = 90F;
            internal static float ladderSpacing = 3F;

            internal static int spokeLadderCount = Mathf.FloorToInt((float)armLength / 3.5F);

            internal static float outermostRimRadius = spokeLadderCount * 3F - rimCount * 3F;

            internal static Vector3 zero = new Vector3(0, 0, 0);

            internal static Vector3 lowWallNorthStart = new Vector3(1.5F - sidesX * 3F / 2F, 0, -sidesZ * 3F / 2F);
            internal static Vector3 lowWallNorthEnd = new Vector3(1.5F + sidesX * 3F / 2F, 0, -sidesZ * 3F / 2F);

            internal static Vector3 lowWallSouthStart = new Vector3(1.5F - sidesX * 3F / 2F, 0, sidesZ * 3F / 2F);
            internal static Vector3 lowWallSouthEnd = new Vector3(1.5F + sidesX * 3F / 2F, 0, sidesZ * 3F / 2F);

            internal static Vector3 lowWallEastStart = lowWallSouthStart;
            internal static Vector3 lowWallEastEnd = lowWallNorthStart;

            internal static Vector3 lowWallWestStart = lowWallSouthEnd;
            internal static Vector3 lowWallWestEnd = lowWallNorthEnd;

            internal static Vector3 supportRotationCorrectionA = new Vector3(-2F, -10F, -5F);
            internal static Vector3 supportRotationCorrectionB = new Vector3(-2F, 10F, 5F);
            internal static Vector3 supportRotationCorrectionC = new Vector3(-2F, 10F, 5F);
            internal static Vector3 supportRotationCorrectionD = new Vector3(-2F, -10F, -5F);

            internal static Vector3 supportsABEnd = new Vector3(0, armLength + 0.25F, -3F);

            internal static Vector3 supportsAstart = new Vector3(-sidesX * 3F / 2F, -1F, -sidesZ * 3F / 2F - 3F);
            internal static Vector3 supportsBstart = new Vector3(sidesX * 3F / 2F, -1F, -sidesZ * 3F / 2F - 3F);

            internal static Vector3 supportsCDEnd = new Vector3(0, armLength + 0.25F, 3F);

            internal static Vector3 supportsCstart = new Vector3(-sidesX * 3F / 2F, -1F, sidesZ * 3F / 2F + 3F);
            internal static Vector3 supportsDstart = new Vector3(sidesX * 3F / 2F, -1F, sidesZ * 3F / 2F + 3F);

            internal static List<Vector3> foundationPositions = new List<Vector3>(AmusementInstance.TheCubeMethod(sidesX, 1, sidesZ, 3F, 0F, 3F, zero, zero).Keys);

            internal static List<Vector3> lowWallPositionsNorth = new List<Vector3>(AmusementInstance.TheLineMethod(lowWallNorthStart, lowWallNorthEnd, false, Vector3.zero, sidesX).Keys);
            internal static List<Vector3> lowWallPositionsSouth = new List<Vector3>(AmusementInstance.TheLineMethod(lowWallSouthStart, lowWallSouthEnd, false, Vector3.zero, sidesX).Keys);

            internal static List<Vector3> lowWallPositionsEast = new List<Vector3>(AmusementInstance.TheLineMethod(lowWallEastStart, lowWallEastEnd, false, Vector3.zero, sidesZ).Keys);
            internal static List<Vector3> lowWallPositionsWest = new List<Vector3>(AmusementInstance.TheLineMethod(lowWallWestStart, lowWallWestEnd, false, Vector3.zero, sidesZ).Keys);

            internal static Dictionary<Vector3, Vector3> supportPositionsTransformsA = AmusementInstance.TheLineMethod(supportsAstart, supportsABEnd, true, supportRotationCorrectionA, 15);
            internal static Dictionary<Vector3, Vector3> supportPositionsTransformsB = AmusementInstance.TheLineMethod(supportsBstart, supportsABEnd, true, supportRotationCorrectionB, 15);
            internal static Dictionary<Vector3, Vector3> supportPositionsTransformsC = AmusementInstance.TheLineMethod(supportsCstart, supportsCDEnd, true, supportRotationCorrectionC, 15);
            internal static Dictionary<Vector3, Vector3> supportPositionsTransformsD = AmusementInstance.TheLineMethod(supportsDstart, supportsCDEnd, true, supportRotationCorrectionD, 15);

            public static List<RideVolumeColliderData> GetRideVolumeColliders()
            {
                return new List<RideVolumeColliderData>
                    {
                        new RideVolumeColliderData
                        {
                            posY = armLength,

                            sizeX = sidesX * 3F + 3F, //+1.5m buffer on each side
                            sizeY = armLength * 2F +3F, //also 1.m buffer below and above
                            sizeZ = sidesZ * 3F + 12F + 3F //account for stairs that stick out and 1.5m buffer
                        }
                    };
            }

            //this can be abstractised further and reused
            public static Dictionary<int, RideDefinitionSegmentEntity> GetSpokeSegmentEntities(int startAt)
            {
                Dictionary<int, RideDefinitionSegmentEntity> result = new Dictionary<int, RideDefinitionSegmentEntity>
                {
                    [FERRIS_ENTITY_OIL_BARREL_1] = new RideDefinitionSegmentEntity
                    {
                        prefabName = PREFAB_OIL_BARREL,
                        posY = armLength - 6.5F,
                        posX = -0.5F,
                        rotZ = 90F,
                        rotX = 90F
                    },

                    [FERRIS_ENTITY_OIL_BARREL_2] = new RideDefinitionSegmentEntity
                    {
                        prefabName = PREFAB_OIL_BARREL,
                        posY = armLength - 6.5F,
                        posX = 0.5F,
                        rotZ = -90F,
                        rotX = 90F
                    }
                };

                for (var i = 0; i < spokeLadderCount; i += 1)
                {
                    result.Add(FERRIS_ENTITY_LADDER_1A + i, new RideDefinitionSegmentEntity
                    {
                        prefabName = PREFAB_LADDER,
                        posX = ladderPosX,
                        posY = 0.5F + (i * ladderSpacing),
                        rotY = ladderRotY
                    });

                    result.Add(FERRIS_ENTITY_LADDER_1B + i + 200, new RideDefinitionSegmentEntity
                    {
                        prefabName = PREFAB_LADDER,
                        posX = -ladderPosX,
                        posY = 0.5F + (i * ladderSpacing),
                        rotY = -ladderRotY
                    });

                    result.Add(FERRIS_ENTITY_XMAS_LIGHTS_1A + i, new RideDefinitionSegmentEntity
                    {
                        prefabName = PREFAB_XMAS_LIGHTS,
                        isSpecial = true,
                        specialGroup = FERRIS_GROUP_XMAS_LIGHT,
                        posX = ladderPosX,
                        posY = 0.5F + (i * ladderSpacing),
                        rotY = ladderRotY,
                        rotZ = 90
                    });

                    result.Add(FERRIS_ENTITY_XMAS_LIGHTS_1A + i + 200, new RideDefinitionSegmentEntity
                    {
                        prefabName = PREFAB_XMAS_LIGHTS,
                        isSpecial = true,
                        specialGroup = FERRIS_GROUP_XMAS_LIGHT,
                        posX = -ladderPosX,
                        posY = 0.5F + (i * ladderSpacing),
                        rotY = -ladderRotY,
                        rotZ = 90

                    });

                }

                return result;
            }

            public static void OutCircleStuff(int ordinal, out float maxCircumference, out float fraction, out float circumference, out float radius, out int cloneCount)
            {
                maxCircumference = 2 * (outermostRimRadius) * Mathf.PI;

                fraction = (float)(ordinal) / (float)rimCount;

                circumference = fraction * maxCircumference;


                radius = (float)(outermostRimRadius) * (float)fraction;

                cloneCount = 3 + Mathf.CeilToInt((float)circumference / 3.25F);
            }

            public static int GetCloneCount(int ordinal)
            {
                float maxCircumference;
                float fraction;
                float circumference;
                float radius;
                int cloneCount;

                OutCircleStuff(ordinal, out maxCircumference, out fraction, out circumference, out radius, out cloneCount);

                return cloneCount;
            }

            public static float GetCloneRadius(int ordinal)
            {
                float maxCircumference;
                float fraction;
                float circumference;
                float radius;
                int cloneCount;

                OutCircleStuff(ordinal, out maxCircumference, out fraction, out circumference, out radius, out cloneCount);

                return radius;
            }

            public static Dictionary<int, RideDefinitionSegmentEntity> GetRimSegmentEntities(int startAt, int ordinal)
            {
                //TESTING:
                var result = new Dictionary<int, RideDefinitionSegmentEntity>();

                float radius;

                float maxCircumference; // = armLength * armLength * Mathf.PI;

                float fraction; // = (float)ordinal / (float)spokeLadderCount;

                float fromCenter; // = new Vector3(0, ((float)armLength * (float)fraction), 0);

                int cloneCount;

                OutCircleStuff(ordinal, out maxCircumference, out fraction, out fromCenter, out radius, out cloneCount);

                result.Add(startAt, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_LADDER,
                    posX = 1.6F,
                    posY = radius,
                    rotY = 90F,
                    rotZ = 90F,
                });

                startAt++;

                result.Add(startAt, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_LADDER,
                    posX = -1.6F,
                    posY = radius,
                    rotY = 90F,
                    rotZ = 90F,
                });

                startAt++;

                result.Add(startAt, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_XMAS_LIGHTS,
                    isSpecial = true,
                    specialGroup = FERRIS_GROUP_XMAS_LIGHT,
                    posX = 1.6F,
                    posY = radius,
                    rotY = 90F,
                });

                startAt++;

                result.Add(startAt, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_XMAS_LIGHTS,
                    isSpecial = true,
                    specialGroup = FERRIS_GROUP_XMAS_LIGHT,
                    posX = -1.6F,
                    posY = radius,
                    rotY = 90F,
                });

                startAt++;

                return result;
            }

            public static Dictionary<int, RideDefinitionSegmentEntity> GetBaseSegmentEntities(int startAt)
            {
                Dictionary<int, RideDefinitionSegmentEntity> result = new Dictionary<int, RideDefinitionSegmentEntity>();

                var currentIndex = startAt;

                foreach (var pos in foundationPositions)
                {
                    result.Add(currentIndex, new RideDefinitionSegmentEntity
                    {
                        prefabName = PREFAB_FOUNDATION,
                        buildingGrade = BuildingGrade.Enum.Wood,
                        isMobile = false,
                        posX = pos.x,
                        posY = pos.y + 1.5F,
                        posZ = pos.z
                    });

                    currentIndex++;
                }

                //some ramps, 2 per side. placed them based on sidesZ
                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_STEPS,
                    buildingGrade = BuildingGrade.Enum.Wood,
                    isMobile = false,
                    posZ = 3F + (sidesZ * 3F / 2F),
                    rotY = 90F,
                    posY = 0,
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_STEPS,
                    buildingGrade = BuildingGrade.Enum.Wood,
                    isMobile = false,
                    posZ = 6F + (sidesZ * 3F / 2F),
                    rotY = 90F,
                    posY = -1.5F,
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_STEPS,
                    buildingGrade = BuildingGrade.Enum.Wood,
                    isMobile = false,
                    posZ = -3F - (sidesZ * 3F / 2F),
                    rotY = 270F,
                    posY = 0,
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_STEPS,
                    buildingGrade = BuildingGrade.Enum.Wood,
                    isMobile = false,
                    posZ = -6F - (sidesZ * 3F / 2F),
                    rotY = 270F,
                    posY = -1.5F
                });

                currentIndex++;

                //now some low walls. North:

                var wallCount = lowWallPositionsNorth.Count;

                var isOdd = wallCount % 2 == 1;

                var middleIndexes = new List<int>();

                if (isOdd)
                {
                    middleIndexes.Add(((wallCount + 1) / 2) - 1);
                }
                else
                {
                    middleIndexes.Add(((wallCount) / 2) - 1);
                    middleIndexes.Add(((wallCount) / 2) + 1);
                }

                var wallIndex = 0;

                foreach (var pos in lowWallPositionsNorth)
                {
                    var prefab = PREFAB_WALL_LOW;

                    if (middleIndexes.Contains(wallIndex))
                    {
                        prefab = PREFAB_WALL_FRAME;
                    }
                    //ignore the middle bit

                    result.Add(currentIndex, new RideDefinitionSegmentEntity
                    {
                        prefabName = prefab,
                        buildingGrade = BuildingGrade.Enum.Wood,
                        isMobile = false,
                        posX = pos.x,
                        posY = pos.y + 1.5F,
                        posZ = pos.z,
                        rotY = 90F
                    });

                    currentIndex++;
                    wallIndex++;
                }

                //south
                wallIndex = 0;
                foreach (var pos in lowWallPositionsSouth)
                {
                    var thisPrefab = PREFAB_WALL_LOW;

                    if (middleIndexes.Contains(wallIndex))
                    {
                        thisPrefab = PREFAB_WALL_FRAME;
                    }

                    result.Add(currentIndex, new RideDefinitionSegmentEntity
                    {
                        prefabName = thisPrefab,
                        buildingGrade = BuildingGrade.Enum.Wood,
                        isMobile = false,
                        posX = pos.x,
                        posY = pos.y + 1.5F,
                        posZ = pos.z,
                        rotY = 270F
                    });

                    currentIndex++;
                    wallIndex++;
                }

                //control room
                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_FLOOR,
                    buildingGrade = BuildingGrade.Enum.Wood,
                    isMobile = false,
                    posX = 0F,
                    posY = 1.5F + 3F,
                    posZ = sidesZ * 3F / 2F - 1.5F,
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_DOORWAY,
                    buildingGrade = BuildingGrade.Enum.Wood,
                    isMobile = false,
                    posX = -1.5F,
                    posY = 1.5F + 3F,
                    posZ = sidesZ * 3F / 2F - 1.5F,
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_DOORWAY,
                    buildingGrade = BuildingGrade.Enum.Wood,
                    isMobile = false,
                    posX = 1.5F,
                    posY = 1.5F + 3F,
                    posZ = sidesZ * 3F / 2F - 1.5F,
                    rotY = 180F
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_DOOR_WOODEN_SINGLE,
                    buildingGrade = BuildingGrade.Enum.Wood,
                    isMobile = false,
                    posX = -1.5F,
                    posY = 1.5F + 3F,
                    posZ = sidesZ * 3F / 2F - 1.5F,
                    rotY = 180F
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_DOOR_WOODEN_SINGLE,
                    buildingGrade = BuildingGrade.Enum.Wood,
                    isMobile = false,
                    posX = 1.5F,
                    posY = 1.5F + 3F,
                    posZ = sidesZ * 3F / 2F - 1.5F,

                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_ROOF_TRIANGLE,
                    buildingGrade = BuildingGrade.Enum.Wood,
                    isMobile = false,
                    posX = 0.6F,
                    posY = 1.5F + 3F + 3F + 0.25F,
                    posZ = sidesZ * 3F / 2F - 1.5F,
                    rotY = 90F,
                    rotX = 20F
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_ROOF_TRIANGLE,
                    buildingGrade = BuildingGrade.Enum.Wood,
                    isMobile = false,
                    posX = -0.6F,
                    posY = 1.5F + 3F + 3F + 0.25F,
                    posZ = sidesZ * 3F / 2F - 1.5F,
                    rotY = 270F,
                    rotX = 20F
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_ROOF_TRIANGLE,
                    buildingGrade = BuildingGrade.Enum.Wood,
                    isMobile = false,
                    posX = 0,
                    posY = 1.5F + 3F + 3F + 0.25F,
                    posZ = sidesZ * 3F / 2F - 1.5F + 0.6F,
                    rotY = 0,
                    rotX = 20F
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_ROOF_TRIANGLE,
                    buildingGrade = BuildingGrade.Enum.Wood,
                    isMobile = false,
                    posX = 0,
                    posY = 1.5F + 3F + 3F + 0.25F,
                    posZ = sidesZ * 3F / 2F - 1.5F - 0.6F,
                    rotY = 180,
                    rotX = 20F
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_WALL_WINDOW,
                    buildingGrade = BuildingGrade.Enum.Wood,
                    isMobile = false,
                    posX = 0F,
                    posY = 1.5F + 3F,
                    posZ = sidesZ * 3F / 2F - 1.5F - 1.5F,

                    rotY = 90F
                });

                currentIndex++;

                //control sign
                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_SIGN_TALL,
                    buildingGrade = BuildingGrade.Enum.Wood,
                    isMobile = false,
                    posX = 1.3F,
                    posY = 1.5F + 3F + 0.5F,
                    posZ = sidesZ * 3F / 2F - 1.5F - 1.5F + 0.118F,

                    isSpecial = true,
                    specialGroup = FERRIS_GROUP_OTHER_SIGNS,

                    rotY = -1.5F,
                    rotZ = 90F
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_WINDOW_SHUTTERS,
                    isMobile = false,
                    posX = 0F,
                    posY = 1.5F + 3F + 1F,
                    posZ = sidesZ * 3F / 2F - 1.5F - 1.5F,

                    rotY = 90F
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_WALL,
                    buildingGrade = BuildingGrade.Enum.Wood,
                    isMobile = false,
                    posX = 0F,
                    posY = 1.5F + 3F,
                    posZ = sidesZ * 3F / 2F - 1.5F + 1.5F,

                    rotY = 270F
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_PICTUREFRAME_XXL,
                    isSpecial = true,
                    specialGroup = FERRIS_GROUP_XXL_SIGNS,
                    isMobile = false,
                    posX = 0F,
                    posY = 1.5F + 3F,
                    posZ = sidesZ * 3F / 2F - 1.5F + 1.5F + 0.4F,
                    rotY = 0F
                });

                currentIndex++;

                //buttons!
                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_BUTTON,
                    isSpecial = true,
                    specialGroup = FERRIS_GROUP_BUTTON_START_STOP,
                    posX = 0F,
                    posY = 1.5F + 2.5F - 0.22F,
                    posZ = sidesZ * 3F / 2F - 1.5F - 1.5F + 0.1F,
                    isMobile = false,

                    rotY = 0F
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_BUTTON,
                    isSpecial = true,
                    specialGroup = FERRIS_GROUP_BUTTON_LIGHTS_ON_OFF,
                    posX = 1.515F,
                    posY = 1.5F + 2.5F + 0.15F,
                    posZ = sidesZ * 3F / 2F - 1.5F - 1.5F + 0.1F,
                    isMobile = false,

                    rotY = 0F,
                    rotZ = 45F
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_BUTTON,
                    isSpecial = true,
                    specialGroup = FERRIS_GROUP_BUTTON_FASTER,
                    posX = -0.43F,
                    posY = 1.5F + 2.5F + 0.125F - 0.23F,
                    posZ = sidesZ * 3F / 2F - 1.5F - 1.5F + 0.06F,
                    isMobile = false,

                    rotY = 0F
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_BUTTON,
                    isSpecial = true,
                    specialGroup = FERRIS_GROUP_BUTTON_SLOWER,
                    posX = -0.43F,
                    posY = 1.5F + 2.5F - 0.125F - 0.23F,
                    posZ = sidesZ * 3F / 2F - 1.5F - 1.5F + 0.06F,
                    isMobile = false,

                    rotY = 0F
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_STEPS,
                    buildingGrade = BuildingGrade.Enum.Wood,
                    isMobile = false,
                    posX = -1.5F - 3F,
                    posY = 1.5F + 3F - 1.5F,
                    posZ = sidesZ * 3F / 2F - 1.5F,
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_STEPS,
                    buildingGrade = BuildingGrade.Enum.Wood,
                    isMobile = false,
                    posX = -1.5F - 3F - 3F,
                    posY = 1.5F + 3F - 1.5F - 1.5F,
                    posZ = sidesZ * 3F / 2F - 1.5F,
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_STEPS,
                    buildingGrade = BuildingGrade.Enum.Wood,
                    isMobile = false,
                    posX = 1.5F + 3F,
                    posY = 1.5F + 3F - 1.5F,
                    posZ = sidesZ * 3F / 2F - 1.5F,

                    rotY = 180F
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_STEPS,
                    buildingGrade = BuildingGrade.Enum.Wood,
                    isMobile = false,
                    posX = 1.5F + 3F + 3F,
                    posY = 1.5F + 3F - 1.5F - 1.5F,
                    posZ = sidesZ * 3F / 2F - 1.5F,

                    rotY = 180F
                });

                currentIndex++;
                //east

                wallIndex = 0;

                foreach (var pos in lowWallPositionsEast)
                {
                    result.Add(currentIndex, new RideDefinitionSegmentEntity
                    {
                        prefabName = PREFAB_WALL_LOW,
                        buildingGrade = BuildingGrade.Enum.Wood,
                        isMobile = false,
                        posX = pos.x - 1.5F,
                        posY = pos.y + 1.5F,
                        posZ = pos.z - 1.5F,
                        rotY = 180F
                    });

                    currentIndex++;
                    wallIndex++;
                }

                //west
                wallIndex = 0;
                foreach (var pos in lowWallPositionsWest)
                {
                    result.Add(currentIndex, new RideDefinitionSegmentEntity
                    {
                        prefabName = PREFAB_WALL_LOW,
                        buildingGrade = BuildingGrade.Enum.Wood,
                        isMobile = false,
                        posX = pos.x - 1.5F,
                        posY = pos.y + 1.5F,
                        posZ = pos.z - 1.5F,
                        rotY = 0F
                    });

                    currentIndex++;
                    wallIndex++;
                }


                //some supports eh
                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_FLOOR_TRIANGLE,
                    buildingGrade = BuildingGrade.Enum.Wood,

                    posX = supportsABEnd.x,
                    posY = supportsABEnd.y - 0.9F,
                    posZ = supportsABEnd.z + 1.1F,


                    rotX = 5F,

                    isMobile = false,

                    rotY = 180F,
                });

                currentIndex++;

                result.Add(currentIndex, new RideDefinitionSegmentEntity
                {
                    prefabName = PREFAB_FLOOR_TRIANGLE,
                    buildingGrade = BuildingGrade.Enum.Wood,

                    posX = supportsCDEnd.x,
                    posY = supportsABEnd.y - 0.9F,
                    posZ = supportsCDEnd.z - 1.1F,

                    rotX = 5F,

                    isMobile = false,

                    rotY = 0F,
                });

                currentIndex++;

                foreach (var transform in supportPositionsTransformsA)
                {
                    result.Add(currentIndex, new RideDefinitionSegmentEntity
                    {
                        prefabName = PREFAB_FLOOR_FRAME,
                        buildingGrade = BuildingGrade.Enum.Wood,
                        isMobile = false,
                        posX = transform.Key.x,
                        posY = transform.Key.y,
                        posZ = transform.Key.z,

                        rotX = transform.Value.x,
                        rotY = transform.Value.y,
                        rotZ = transform.Value.z
                    });

                    currentIndex++;
                }

                foreach (var transform in supportPositionsTransformsB)
                {
                    result.Add(currentIndex, new RideDefinitionSegmentEntity
                    {
                        prefabName = PREFAB_FLOOR_FRAME,
                        buildingGrade = BuildingGrade.Enum.Wood,
                        isMobile = false,
                        posX = transform.Key.x,
                        posY = transform.Key.y,
                        posZ = transform.Key.z,

                        rotX = transform.Value.x,
                        rotY = transform.Value.y,
                        rotZ = transform.Value.z
                    });

                    currentIndex++;
                }

                foreach (var transform in supportPositionsTransformsC)
                {
                    result.Add(currentIndex, new RideDefinitionSegmentEntity
                    {
                        prefabName = PREFAB_FLOOR_FRAME,
                        buildingGrade = BuildingGrade.Enum.Wood,
                        isMobile = false,
                        posX = transform.Key.x,
                        posY = transform.Key.y,
                        posZ = transform.Key.z,

                        rotX = transform.Value.x,
                        rotY = transform.Value.y,
                        rotZ = transform.Value.z
                    });

                    currentIndex++;
                }

                foreach (var transform in supportPositionsTransformsD)
                {
                    result.Add(currentIndex, new RideDefinitionSegmentEntity
                    {
                        prefabName = PREFAB_FLOOR_FRAME,
                        buildingGrade = BuildingGrade.Enum.Wood,
                        isMobile = false,
                        posX = transform.Key.x,
                        posY = transform.Key.y,
                        posZ = transform.Key.z,

                        rotX = transform.Value.x,
                        rotY = transform.Value.y,
                        rotZ = transform.Value.z
                    });

                    currentIndex++;
                }

                return result;
            }


        }
        #endregion
        #region FERRIS WHEEL DEFINITION
        public RideDefinition DefaultDefinitionFerrisWheel()
        {
            return new RideDefinition
            {
                rideName = "FerrisWheel",
                rideNickname = "Ferris Wheel",
                rideDescription = "This 50 meter tall ride makes for a marvelous landmark. This ride comes equipped with a control room. Lights can be toggled.",
                rideImage = "https://i.imgur.com/mVLGdRB.png",
                rideVersion = RIDE_VERSION,

                handlerName = "FerrisWheel",
                rideCost = new Dictionary<int, int>
                {
                    [ITEM_WOOD] = 10000
                },

                workbenchLevel = 3,

                assignedMusic = new List<string> { "ferriswheel" },


                specialSignData = new Dictionary<int, Dictionary<int, string>>
                {
                    [FERRIS_GROUP_XXL_SIGNS] = new Dictionary<int, string>
                    {
                        [0] = "https://i.imgur.com/EF5Y06C.png",
                    },

                    [FERRIS_GROUP_GONDOLA_SIGNS] = new Dictionary<int, string>
                    {
                        [0] = "https://i.imgur.com/kR2YeJD.png"
                    },

                    [FERRIS_GROUP_OTHER_SIGNS] = new Dictionary<int, string>
                    {
                        [0] = "https://i.imgur.com/sC0BZAq.png"
                    }
                },

                minWaitTime = 3F,
                influenceRadius = 200F,
                admissionFeePrice = 10,
                hasBuilding = true,
                containerType = RideContainerType.TC,

                posContainerX = 0F,
                posContainerY = 1.5F + 3F + 3F,
                posContainerZ = DefinitionFactoryFerrisWheel.sidesZ * 3F / 2F - 1.5F + 1F,

                posMusicX = 0F,
                posMusicY = 1.5F + 3F + 0.2F,
                posMusicZ = DefinitionFactoryFerrisWheel.sidesZ * 3F / 2F - 1.5F + 1.25F,

                rotMusicY = 180F,

                rotContainerY = 180F,
                rotContainerX = 20F,

                posFoundationCorrectionY = -1.5F,
                posAntiCorrectionY = 1.5F,

                containerHealth = 3000F,

                rideItemSkinID = 2262053034,
                rideItemPickupID = ITEM_TC,

                canJoinLate = true,

                rideVolumes = DefinitionFactoryFerrisWheel.GetRideVolumeColliders(),

                extraData = new ExtraRideData
                {
                    memoryBool = new Dictionary<int, bool>
                    {
                        [FERRIS_MEMORY_LIGHTS_ON] = false,
                    },

                    memoryInt = new Dictionary<int, int>
                    {
                        [FERRIS_MEMORY_SPEED] = 1
                    }
                },

                definitionSegments = new Dictionary<int, RideDefinitionSegment>
                {
                    [FERRIS_SEGMENT_PILLAR_A] = new RideDefinitionSegment
                    {
                        name = "Base",

                        segmentEntities = DefinitionFactoryFerrisWheel.GetBaseSegmentEntities(28),

                        childSegments = new Dictionary<int, RideDefinitionSegment>
                        {
                            [FERRIS_SEGMENT_ROTOR_SOCKET] = new RideDefinitionSegment
                            {
                                name = "RotorSocket",
                                pivotPosY = DefinitionFactoryFerrisWheel.armLength - 0.75F,
                                pivotRotX = 90F,
                                pivotItemID = -1021495308, //spring

                                segmentEntities = new Dictionary<int, RideDefinitionSegmentEntity>
                                {

                                    [FERRIS_ENTITY_SPEAKER_BARREL] = new RideDefinitionSegmentEntity
                                    {
                                        prefabName = PREFAB_SPEAKER,
                                    },

                                    [FERRIS_ENTITY_HOBO_BARREL_1] = new RideDefinitionSegmentEntity
                                    {
                                        prefabName = PREFAB_DIESEL_BARREL,
                                        posY = 1.1F,
                                        rotZ = -180F
                                    },

                                    [FERRIS_ENTITY_HOBO_BARREL_2] = new RideDefinitionSegmentEntity
                                    {
                                        prefabName = PREFAB_DIESEL_BARREL,
                                        posY = -1.1F,
                                        rotZ = 0F,
                                    },
                                    [FERRIS_ENTITY_HOBO_BARREL_3] = new RideDefinitionSegmentEntity
                                    {
                                        prefabName = PREFAB_DIESEL_BARREL,
                                        posY = 2.2F,
                                        rotZ = -180F
                                    },

                                    [FERRIS_ENTITY_HOBO_BARREL_4] = new RideDefinitionSegmentEntity
                                    {
                                        prefabName = PREFAB_DIESEL_BARREL,
                                        posY = -2.2F,
                                        rotZ = 0F
                                    },
                                },

                                childSegments = new Dictionary<int, RideDefinitionSegment>
                                {
                                    [FERRIS_SEGMENT_ROTOR_PLUG] = new RideDefinitionSegment
                                    {
                                        name = "RotorPlug",

                                        childSegments = new Dictionary<int, RideDefinitionSegment>
                                        {
                                            //ideally those 2 should be moved to the factory. the factory can take the number of arms into account.

                                            [FERRIS_SEGMENT_SPOKE] = new RideDefinitionSegment
                                            {
                                                name = "Spoke",
                                                cloneCount = DefinitionFactoryFerrisWheel.armCount,
                                                //cloneRotY = RideHandlerFerrisWheel.DefinitionFactory
                                                clonePosX = -0.5F,
                                                cloneRotZ = 90F,

                                                segmentEntities = DefinitionFactoryFerrisWheel.GetSpokeSegmentEntities(FERRIS_ENTITY_OIL_BARREL_1)
                                            },

                                            [FERRIS_SEGMENT_RIM] = new RideDefinitionSegment
                                            {
                                                name = "RimA",
                                                cloneCount = DefinitionFactoryFerrisWheel.GetCloneCount(1),

                                                //clonePosY = RideHandlerFerrisWheel.DefinitionFactory.GetCloneRadius(1),
                                                clonePosX = -0.5F,
                                                cloneRotZ = 90F,

                                                segmentEntities = DefinitionFactoryFerrisWheel.GetRimSegmentEntities(FERRIS_ENTITY_LADDER_1A, 1),
                                            },

                                            [FERRIS_SEGMENT_RIM + 1] = new RideDefinitionSegment
                                            {
                                                name = "RimB",
                                                cloneCount = DefinitionFactoryFerrisWheel.GetCloneCount(2),
                                                //clonePosY = RideHandlerFerrisWheel.DefinitionFactory.GetCloneRadius(2),

                                                clonePosX = -0.5F,
                                                cloneRotZ = 90F,

                                                segmentEntities = DefinitionFactoryFerrisWheel.GetRimSegmentEntities(FERRIS_ENTITY_LADDER_1A, 2),
                                            },

                                            [FERRIS_SEGMENT_GONDOLA] = new RideDefinitionSegment
                                            {
                                                name = "Gondola",
                                                cloneCount = DefinitionFactoryFerrisWheel.armCount,
                                                clonePosX = DefinitionFactoryFerrisWheel.armLength - 6F,
                                                clonesFaceSameDirection = true,
                                                sameRotX = -90F,
                                                pivotItemID = 1491189398, //paddle

                                                hasCollider = true,
                                                colliderPosY = -2.5F,
                                                colliderSizeX = 2.5F,
                                                colliderSizeY = 2.4F,
                                                colliderSizeZ = 2.5F,

                                                segmentEntities = new Dictionary<int, RideDefinitionSegmentEntity>
                                                {
                                                    [FERRIS_ENTITY_SHOTGUN_TRAP] = new RideDefinitionSegmentEntity
                                                    {
                                                        prefabName = PREFAB_SHOTGUN_TRAP,

                                                        posY = -1F,
                                                        posZ = -0.12F,
                                                        locked = true,
                                                    },

                                                    [FERRIS_ENTITY_SPEAKER] = new RideDefinitionSegmentEntity
                                                    {
                                                        prefabName = PREFAB_SPEAKER,
                                                        posX = 0F,
                                                        posY = -0.78F,
                                                        posZ = -0.10F,

                                                        rotZ = 270F,
                                                    },

                                                    [FERRIS_ENTITY_ROOF_LEFT] = new RideDefinitionSegmentEntity
                                                    {
                                                        prefabName = PREFAB_SIGN_WOODEN_LARGE,
                                                        posX = 0.01F,
                                                        posY = -0.88F,

                                                        rotX = 305F,
                                                        rotY = 270F,
                                                        rotZ = 180F,

                                                        isSpecial = true,
                                                        specialGroup = FERRIS_GROUP_GONDOLA_SIGNS
                                                    },

                                                    [FERRIS_ENTITY_ROOF_RIGHT] = new RideDefinitionSegmentEntity
                                                    {
                                                        prefabName = PREFAB_SIGN_WOODEN_LARGE,
                                                        posX = -0.01F,
                                                        posY = -0.88F,

                                                        rotX = 305F,
                                                        rotY = 90F,
                                                        rotZ = 180F,

                                                        isSpecial = true,
                                                        specialGroup = FERRIS_GROUP_GONDOLA_SIGNS
                                                    },

                                                    [FERRIS_ENTITY_FLOOR_LEFT] = new RideDefinitionSegmentEntity
                                                    {
                                                        prefabName = PREFAB_SIGN_WOODEN_LARGE,

                                                        posY = -3.61F,

                                                        rotX = 270F,
                                                        rotY = 90F,

                                                        isSpecial = true,
                                                        specialGroup = FERRIS_GROUP_GONDOLA_SIGNS
                                                    },

                                                    [FERRIS_ENTITY_FLOOR_RIGHT] = new RideDefinitionSegmentEntity
                                                    {
                                                        prefabName = PREFAB_SIGN_WOODEN_LARGE,

                                                        posY = -3.61F,

                                                        rotX = 270F,
                                                        rotY = 270F,

                                                        isSpecial = true,
                                                        specialGroup = FERRIS_GROUP_GONDOLA_SIGNS
                                                    },
                                                    
                                                    [FERRIS_ENTITY_PILLAR_BOTTOM_LEFT_BACK] = new RideDefinitionSegmentEntity
                                                    {
                                                        propItemID = ITEM_WOOD,

                                                        posX = -1.4F,
                                                        posY = -3.1F,
                                                        posZ = -1.4F,

                                                        rotY = 180F,
                                                        rotZ = 90F,
                                                    },

                                                    [FERRIS_ENTITY_PILLAR_BOTTOM_RIGHT_BACK] = new RideDefinitionSegmentEntity
                                                    {
                                                        propItemID = ITEM_WOOD,

                                                        posX = 1.4F,
                                                        posY = -3.1F,
                                                        posZ = -1.4F,

                                                        rotZ = 90F,
                                                    },

                                                    [FERRIS_ENTITY_PILLAR_BOTTOM_LEFT_FRONT] = new RideDefinitionSegmentEntity
                                                    {
                                                        propItemID = ITEM_WOOD,

                                                        posX = -1.4F,
                                                        posY = -3.1F,
                                                        posZ = 1.4F,

                                                        rotY = 180F,
                                                        rotZ = 90F,
                                                    },

                                                    [FERRIS_ENTITY_PILLAR_BOTTOM_RIGHT_FRONT] = new RideDefinitionSegmentEntity
                                                    {
                                                        propItemID = ITEM_WOOD,

                                                        posX = 1.4F,
                                                        posY = -3.1F,
                                                        posZ = 1.4F,


                                                        rotZ = 90F,
                                                    },

                                                    [FERRIS_ENTITY_PILLAR_TOP_LEFT_BACK] = new RideDefinitionSegmentEntity
                                                    {
                                                        propItemID = ITEM_WOOD,

                                                        posX = -1.256F,
                                                        posY = -2.092F,
                                                        posZ = -1.4F,

                                                        rotX = 0F,
                                                        rotY = 0F,
                                                        rotZ = 75F,
                                                    },

                                                    [FERRIS_ENTITY_PILLAR_TOP_RIGHT_BACK] = new RideDefinitionSegmentEntity
                                                    {
                                                        propItemID = ITEM_WOOD,

                                                        posX = 1.256F,
                                                        posY = -2.092F,
                                                        posZ = -1.4F,

                                                        rotX = 0F,
                                                        rotY = 180F,
                                                        rotZ = 75F,
                                                    },

                                                    [FERRIS_ENTITY_PILLAR_TOP_LEFT_FRONT] = new RideDefinitionSegmentEntity
                                                    {
                                                        propItemID = ITEM_WOOD,

                                                        posX = -1.256F,
                                                        posY = -2.092F,
                                                        posZ = 1.4F,

                                                        rotX = 0F,
                                                        rotY = 0F,
                                                        rotZ = 75F,
                                                    },

                                                    [FERRIS_ENTITY_PILLAR_TOP_RIGHT_FRONT] = new RideDefinitionSegmentEntity
                                                    {
                                                        propItemID = ITEM_WOOD,

                                                        posX = 1.256F,
                                                        posY = -2.092F,
                                                        posZ = 1.4F,

                                                        rotX = 0F,
                                                        rotY = 180F,
                                                        rotZ = 75F,
                                                    },

                                                    

                                                    [FERRIS_ENTITY_XMAS_BOTTOM_LEFT] = new RideDefinitionSegmentEntity
                                                    {
                                                        prefabName = PREFAB_XMAS_LIGHTS,
                                                        isSpecial = true,
                                                        specialGroup = FERRIS_GROUP_XMAS_LIGHT,

                                                        posX = -1.5F,
                                                        posY = -3.711F,
                                                        posZ = 0.01F,

                                                        rotY = 270F,
                                                    },

                                                    [FERRIS_ENTITY_XMAS_BOTTOM_RIGHT] = new RideDefinitionSegmentEntity
                                                    {
                                                        prefabName = PREFAB_XMAS_LIGHTS,
                                                        isSpecial = true,
                                                        specialGroup = FERRIS_GROUP_XMAS_LIGHT,

                                                        posX = 1.5F,
                                                        posY = -3.711F,
                                                        posZ = 0.01F,

                                                        rotY = 90F,
                                                    },

                                                    [FERRIS_ENTITY_XMAS_BOTTOM_FRONT] = new RideDefinitionSegmentEntity
                                                    {
                                                        prefabName = PREFAB_XMAS_LIGHTS,
                                                        isSpecial = true,
                                                        specialGroup = FERRIS_GROUP_XMAS_LIGHT,

                                                        posX = 0F,
                                                        posY = -3.711F,
                                                        posZ = 1.5F,
                                                    },

                                                    [FERRIS_ENTITY_XMAS_BOTTOM_BACK] = new RideDefinitionSegmentEntity
                                                    {
                                                        prefabName = PREFAB_XMAS_LIGHTS,
                                                        isSpecial = true,
                                                        specialGroup = FERRIS_GROUP_XMAS_LIGHT,

                                                        posX = 0F,
                                                        posY = -3.711F,
                                                        posZ = -1.5F,

                                                        rotY = 180F
                                                    },

                                                    [FERRIS_ENTITY_XMAS_TOP_LEFT] = new RideDefinitionSegmentEntity
                                                    {
                                                        prefabName = PREFAB_XMAS_LIGHTS,
                                                        isSpecial = true,
                                                        specialGroup = FERRIS_GROUP_XMAS_LIGHT,

                                                        posX = -1.24F,
                                                        posY = -1.759F,
                                                        posZ = 0.01F,

                                                        rotY = 270F
                                                    },

                                                    [FERRIS_ENTITY_XMAS_TOP_CENTER] = new RideDefinitionSegmentEntity
                                                    {
                                                        prefabName = PREFAB_XMAS_LIGHTS,
                                                        isSpecial = true,
                                                        specialGroup = FERRIS_GROUP_XMAS_LIGHT,

                                                        posX = 0F,
                                                        posY = -0.947F,
                                                        posZ = 0.01F,

                                                        rotY = 90F
                                                    },

                                                    [FERRIS_ENTITY_XMAS_TOP_RIGHT] = new RideDefinitionSegmentEntity
                                                    {
                                                        prefabName = PREFAB_XMAS_LIGHTS,
                                                        isSpecial = true,
                                                        specialGroup = FERRIS_GROUP_XMAS_LIGHT,

                                                        posX = 1.24F,
                                                        posY = -1.759F,
                                                        posZ = 0.01F,

                                                        rotY = 90F
                                                    },
                                                },
                                                    

                                                childSegments = new Dictionary<int, RideDefinitionSegment>
                                                {
                                                    [FERRIS_SEGMENT_WALL] = new RideDefinitionSegment
                                                    {
                                                        name = "Wall",
                                                        cloneCount = 4,

                                                        clonePosX = 1.5F,
                                                        clonePosY = -3.6F,

                                                        clonePosZ = 1.5F,

                                                        cloneRotZ = 90F,

                                                        pivotItemID = 1401987718, //tape

                                                        segmentEntities = new Dictionary<int, RideDefinitionSegmentEntity>
                                                        {
                                                            [FERRIS_ENTITY_WALL_SIGN] = new RideDefinitionSegmentEntity
                                                            {
                                                                prefabName = PREFAB_SIGN_TALL,

                                                                posY = 0.2F,
                                                                posX = 0.48F,
                                                                posZ = -0.06F,

                                                                rotX = -1.25F, //signs are stupid

                                                                /*
                                                                posY = -3.8F + 0.6F,
                                                                posX = 1.3F,
                                                                posZ = 1.47F,

                                                                rotX = 180,

                                                                rotY = 180F,

                                                                rotZ = 270F,
                                                                */

                                                                isSpecial = true,
                                                                specialGroup = FERRIS_GROUP_GONDOLA_SIGNS

                                                            },
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    },
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
