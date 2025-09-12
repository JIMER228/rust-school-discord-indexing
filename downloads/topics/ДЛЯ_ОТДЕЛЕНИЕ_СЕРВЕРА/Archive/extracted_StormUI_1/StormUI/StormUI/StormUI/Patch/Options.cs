using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace StormUI.Patch
{
    // Удаление двух последних чекбоксов из вкладки бустера
    [HarmonyPatch(typeof(TweakUIToggle), "Init")]
    public class TweakUIToggle_Patch
    {
        static void Prefix(TweakUIToggle __instance)
        {
            if (__instance.name == "Contact Shadows" || __instance.name == "Sustain MIDI Input")
            {
                foreach (MaskableGraphic maskableGraphic in __instance.GetComponentsInChildren<MaskableGraphic>())
                {
                    Object.Destroy(maskableGraphic);
                }
            }

            if (__instance.name == "Occlusion Culling")
            {
                __instance.convarName = "booster.noshadows";
                __instance.toggleControl.onValueChanged.AddListener(new System.Action<bool>((bool val) =>
                {
                    Defines.NoShadowsCommand.Set(val ? "1" : "0");
                }));
            }

            if (__instance.name == "Grass Shadows")
            {
                __instance.convarName = "booster.nograss";
                __instance.toggleControl.onValueChanged.AddListener(new System.Action<bool>((bool val) =>
                {
                    Defines.NoGrassCommand.Set(val ? "1" : "0");
                }));
            }
        }
    }

    public class TweakUIToggle_Patch2
    {
        public static bool Prefix(TweakUIToggle __instance)
        {
            if (__instance.name == "Occlusion Culling")
            {
                __instance.toggleControl.isOn = Defines._noShadows;
                return false;
            }

            if (__instance.name == "Grass Shadows")
            {
                __instance.toggleControl.isOn = Defines._noGrass;
                return false;
            }

            return true;
        }
    }
}
