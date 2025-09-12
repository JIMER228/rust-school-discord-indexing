using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BountyBooster.Models;
using HarmonyLib;

namespace BountyBooster
{
	// Token: 0x02000002 RID: 2
	[global::BepInEx.BepInPlugin("com.bounty.booster", "BountyBooster", "1.0.0")]
	public class BountyPlugin : global::BepInEx.Unity.IL2CPP.BasePlugin
	{
		// Token: 0x06000001 RID: 1 RVA: 0x00002050 File Offset: 0x00000250
		public override void Load()
		{
			base.AddComponent<global::BountyBooster.Manager>();
			global::HarmonyLib.Harmony.CreateAndPatchAll(typeof(global::BountyBooster.BountyPlugin).Assembly, null);
		}

		// Token: 0x06000002 RID: 2 RVA: 0x0000206F File Offset: 0x0000026F
		public static bool IsOfficialServer(string address)
		{
			return !string.IsNullOrEmpty(address) && !string.IsNullOrEmpty(global::BountyBooster.BountyPlugin.validAddress) && address.Equals(global::BountyBooster.BountyPlugin.validAddress);
		}

		// Token: 0x06000003 RID: 3 RVA: 0x00002092 File Offset: 0x00000292
		public BountyPlugin()
		{
		}

		// Token: 0x04000001 RID: 1
		public static global::BountyBooster.Models.Manifest manifest;

		// Token: 0x04000002 RID: 2
		public static global::ServerBrowserList browserList;

		// Token: 0x04000003 RID: 3
		public static string validAddress;
	}
}
