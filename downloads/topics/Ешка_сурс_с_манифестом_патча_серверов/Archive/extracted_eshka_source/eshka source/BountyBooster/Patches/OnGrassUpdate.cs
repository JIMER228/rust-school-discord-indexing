using System;
using HarmonyLib;

namespace BountyBooster.Patches
{
	// Token: 0x02000009 RID: 9
	[global::HarmonyLib.HarmonyPatch(typeof(global::FoliageCell), "CalculateLOD")]
	public class OnGrassUpdate
	{
		// Token: 0x06000024 RID: 36 RVA: 0x000020AE File Offset: 0x000002AE
		public OnGrassUpdate()
		{
		}

		// Token: 0x06000025 RID: 37 RVA: 0x00002BE0 File Offset: 0x00000DE0
		private static bool Prefix(ref float __result)
		{
			bool result;
			if (!global::BountyBooster.Globals.NoGrass)
			{
				result = true;
			}
			else
			{
				__result = 1f;
				result = false;
			}
			return result;
		}
	}
}
