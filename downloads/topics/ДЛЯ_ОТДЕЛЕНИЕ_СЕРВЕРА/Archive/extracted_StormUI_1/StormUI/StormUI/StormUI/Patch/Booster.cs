using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace StormUI.Patch
{
    [HarmonyPatch(typeof(SunSettings), "Update")] // patch to disable shadows
    #region SunSettingsUpdate
    public class SunSettings_Patch
    {
        static bool Prefix(SunSettings __instance)
        {
            if (Defines._noShadows && !MainMenuSystem.isOpen)
            {
                __instance.light.shadows = LightShadows.None;
                return false;
            }

            return true;
        }
    }
    #endregion

    [HarmonyPatch(typeof(FoliageCell), "CalculateLOD")] // patch to disable grass
    #region FoliageCell CalculateLOD
    public class FoliageCell_Patch
    {
        static bool Prefix(ref float __result)
        {
            if (Defines._noGrass)
            {
                __result = 1f;
                return false;
            }

            return true;
        }
    }
    #endregion
}
