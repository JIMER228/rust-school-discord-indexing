using System;
using System.IO;
using HarmonyLib;
using Il2CppSystem.IO;
using Rust.Platform.Steam;
using Rust.Workshop;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace BountyBooster.Patches
{
	// Token: 0x0200000C RID: 12
	public class Skins
	{
		// Token: 0x0600002A RID: 42 RVA: 0x000020AE File Offset: 0x000002AE
		public Skins()
		{
		}

		// Token: 0x02000019 RID: 25
		[global::HarmonyLib.HarmonyPatch(typeof(global::LoadingScreen), "UpdateFromServer")]
		internal static class UpdateFromServerPatch
		{
			// Token: 0x06000042 RID: 66 RVA: 0x00002F64 File Offset: 0x00001164
			[global::HarmonyLib.HarmonyPrefix]
			internal static void Prefix()
			{
				global::BountyBooster.Patches.Skins.UpdateFromServerPatch.SetupWorkshop();
			}

			// Token: 0x06000043 RID: 67 RVA: 0x00002F6C File Offset: 0x0000116C
			private static void SetupWorkshop()
			{
				global::Rust.Workshop.WorkshopSkin.DownloadTimeout = 1f;
				string text = global::UnityEngine.Application.dataPath;
				text = text.Remove(global::UnityEngine.Application.dataPath.Length - 0x10);
				text += "\\workshop";
				if (!global::System.IO.Directory.Exists(text))
				{
					global::System.IO.Directory.CreateDirectory(text);
				}
				foreach (string text2 in global::System.IO.Directory.GetDirectories(text))
				{
					try
					{
						global::Rust.Workshop.WorkshopSkin.LoadFromWorkshop(ulong.Parse(new global::Il2CppSystem.IO.DirectoryInfo(text2).Name));
					}
					catch
					{
					}
				}
			}
		}

		// Token: 0x0200001A RID: 26
		[global::HarmonyLib.HarmonyPatch(typeof(global::ItemBlueprint), "get_NeedsSteamDLC")]
		internal static class SteamDlcPatch
		{
			// Token: 0x06000044 RID: 68 RVA: 0x00002FFC File Offset: 0x000011FC
			[global::HarmonyLib.HarmonyPrefix]
			public static bool Prefix(ref bool __result)
			{
				__result = false;
				return false;
			}
		}

		// Token: 0x0200001B RID: 27
		[global::HarmonyLib.HarmonyPatch(typeof(global::SteamDLCItem), "IsInstalled")]
		internal static class InstalledPatch
		{
			// Token: 0x06000045 RID: 69 RVA: 0x00003002 File Offset: 0x00001202
			[global::HarmonyLib.HarmonyPrefix]
			public static bool Prefix(ref bool __result)
			{
				__result = true;
				return false;
			}
		}

		// Token: 0x0200001C RID: 28
		[global::HarmonyLib.HarmonyPatch(typeof(global::Steamworks.ISteamUGC), "GetItemInstallInfo")]
		internal static class WorkshopGetItemPatch
		{
			// Token: 0x06000046 RID: 70 RVA: 0x00003008 File Offset: 0x00001208
			[global::HarmonyLib.HarmonyPostfix]
			public static void Postfix(global::Steamworks.Data.PublishedFileId nPublishedFileID, ref ulong punSizeOnDisk, ref string pchFolder, ref uint punTimeStamp, ref bool __result)
			{
				global::BountyBooster.Patches.Skins.WorkshopGetItemPatch.Patch(nPublishedFileID, ref punSizeOnDisk, ref pchFolder, ref punTimeStamp, ref __result);
			}

			// Token: 0x06000047 RID: 71 RVA: 0x00003018 File Offset: 0x00001218
			private static void Patch(global::Steamworks.Data.PublishedFileId nPublishedFileID, ref ulong punSizeOnDisk, ref string pchFolder, ref uint punTimeStamp, ref bool __result)
			{
				pchFolder = string.Empty;
				string text = global::UnityEngine.Application.dataPath;
				text = text.Remove(global::UnityEngine.Application.dataPath.Length - 0x10);
				text = text + "\\workshop\\" + nPublishedFileID.Value.ToString();
				if (global::System.IO.Directory.Exists(text))
				{
					pchFolder = text;
					__result = true;
				}
			}
		}

		// Token: 0x0200001D RID: 29
		[global::HarmonyLib.HarmonyPatch(typeof(global::Steamworks.ISteamUGC), "DownloadItem")]
		internal static class WorkshopDownloadItemPatch
		{
			// Token: 0x06000048 RID: 72 RVA: 0x0000306D File Offset: 0x0000126D
			[global::HarmonyLib.HarmonyPrefix]
			public static bool Prefix(global::Steamworks.Data.PublishedFileId nPublishedFileID, bool bHighPriority, ref bool __result)
			{
				if (global::System.IO.Directory.Exists(global::UnityEngine.Application.dataPath.Remove(global::UnityEngine.Application.dataPath.Length - 0x10) + "\\workshop\\" + nPublishedFileID.Value.ToString()))
				{
					__result = true;
					return false;
				}
				return true;
			}
		}

		// Token: 0x0200001E RID: 30
		[global::HarmonyLib.HarmonyPatch(typeof(global::Rust.Platform.Steam.SteamWorkshopContent), "Download")]
		internal static class WorkshopDownloadExItemPatch
		{
			// Token: 0x06000049 RID: 73 RVA: 0x000030AC File Offset: 0x000012AC
			[global::HarmonyLib.HarmonyPrefix]
			public static bool Prefix(global::Rust.Platform.Steam.SteamWorkshopContent __instance, ref bool __result)
			{
				if (global::System.IO.Directory.Exists(global::UnityEngine.Application.dataPath.Remove(global::UnityEngine.Application.dataPath.Length - 0x10) + "\\workshop\\" + __instance.WorkshopId.ToString()))
				{
					__result = true;
					return false;
				}
				return true;
			}
		}

		// Token: 0x0200001F RID: 31
		[global::HarmonyLib.HarmonyPatch(typeof(global::Steamworks.ISteamUGC), "GetItemState")]
		internal static class ItemStatePatch
		{
			// Token: 0x0600004A RID: 74 RVA: 0x000030F5 File Offset: 0x000012F5
			[global::HarmonyLib.HarmonyPrefix]
			public static bool Prefix(global::Steamworks.Data.PublishedFileId nPublishedFileID, ref uint __result)
			{
				if (global::System.IO.Directory.Exists(global::UnityEngine.Application.dataPath.Remove(global::UnityEngine.Application.dataPath.Length - 0x10) + "\\workshop\\" + nPublishedFileID.Value.ToString()))
				{
					__result = 4U;
					return false;
				}
				return true;
			}
		}

		// Token: 0x02000020 RID: 32
		[global::HarmonyLib.HarmonyPatch(typeof(global::Rust.Workshop.Skin), "LoadIcon")]
		internal static class WorkshopLoadIconPatch
		{
			// Token: 0x0600004B RID: 75 RVA: 0x00003134 File Offset: 0x00001334
			[global::HarmonyLib.HarmonyPrefix]
			public static void Prefix(global::Rust.Workshop.Skin __instance, ulong workshopId, ref string directory, global::UnityEngine.AssetBundle bundle = null)
			{
				string text = global::UnityEngine.Application.dataPath;
				text = text.Remove(global::UnityEngine.Application.dataPath.Length - 0x10);
				text = text + "\\workshop\\" + workshopId.ToString();
				directory = text;
			}
		}

		// Token: 0x02000021 RID: 33
		[global::HarmonyLib.HarmonyPatch(typeof(global::Rust.Workshop.Skin), "LoadAssets")]
		internal static class WorkshopLoadAssetsnPatch
		{
			// Token: 0x0600004C RID: 76 RVA: 0x00003174 File Offset: 0x00001374
			[global::HarmonyLib.HarmonyPrefix]
			public static void Prefix(global::Rust.Workshop.Skin __instance, ulong workshopId, ref string directory, global::UnityEngine.AssetBundle bundle = null)
			{
				string text = global::UnityEngine.Application.dataPath;
				text = text.Remove(global::UnityEngine.Application.dataPath.Length - 0x10);
				text = text + "\\workshop\\" + workshopId.ToString();
				directory = text;
			}
		}
	}
}
