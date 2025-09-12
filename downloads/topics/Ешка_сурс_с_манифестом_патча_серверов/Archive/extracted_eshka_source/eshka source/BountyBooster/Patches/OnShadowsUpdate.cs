using System;
using HarmonyLib;

namespace BountyBooster.Patches
{
	// Token: 0x0200000A RID: 10
	[global::HarmonyLib.HarmonyPatch(typeof(global::SunSettings), "Update")]
	public class OnShadowsUpdate
	{
		// Token: 0x06000026 RID: 38 RVA: 0x000020AE File Offset: 0x000002AE
		public OnShadowsUpdate()
		{
		}

		// Token: 0x06000027 RID: 39 RVA: 0x00002C04 File Offset: 0x00000E04
		private static bool Prefix(global::SunSettings __instance)
		{
			bool result;
			if (global::BountyBooster.Globals.NoShadows && !global::MainMenuSystem.isOpen)
			{
				__instance.light.shadows = 0;
				result = false;
			}
			else
			{
				result = true;
			}
			return result;
		}
	}
}
