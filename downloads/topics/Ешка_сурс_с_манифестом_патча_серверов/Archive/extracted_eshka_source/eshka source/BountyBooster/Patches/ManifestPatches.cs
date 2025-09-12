using System;
using System.Net;
using BountyBooster.Models;
using ConVar;
using Facepunch;
using HarmonyLib;
using Newtonsoft.Json;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.UI;

namespace BountyBooster.Patches
{
	// Token: 0x0200000B RID: 11
	public class ManifestPatches
	{
		// Token: 0x06000028 RID: 40 RVA: 0x00002C38 File Offset: 0x00000E38
		private static void HideCategory(global::ServerBrowserList category)
		{
			category.categoryButton.enabled = false;
			category.enabled = false;
			category.categoryButton.gameObject.SetActive(false);
			category.gameObject.SetActive(false);
			foreach (global::UnityEngine.UI.Text text in category.categoryButton.GetComponents<global::UnityEngine.UI.Text>())
			{
				text.text = "";
			}
			global::UnityEngine.Object.DestroyImmediate((global::UnityEngine.Object)category.categoryButton.gameObject);
			global::UnityEngine.Object.DestroyImmediate((global::UnityEngine.Object)category.gameObject);
		}

		// Token: 0x06000029 RID: 41 RVA: 0x000020AE File Offset: 0x000002AE
		public ManifestPatches()
		{
		}

		// Token: 0x02000010 RID: 16
		[global::HarmonyLib.HarmonyPatch(typeof(global::Facepunch.Manifest), "LoadManifest")]
		internal static class LoadManifestPatch
		{
			// Token: 0x06000034 RID: 52 RVA: 0x00002D70 File Offset: 0x00000F70
			[global::HarmonyLib.HarmonyPrefix]
			public static bool Prefix(ref string text)
			{
				global::BountyBooster.Patches.ManifestPatches.LoadManifestPatch.Load(ref text);
				return true;
			}

			// Token: 0x06000035 RID: 53 RVA: 0x00002D7C File Offset: 0x00000F7C
			private static void Load(ref string a)
			{
				a = new global::System.Net.WebClient().DownloadString("https://raw.githubusercontent.com/tishka77/bountymanifest/refs/heads/main/bountyjson");
				try
				{
					global::BountyBooster.BountyPlugin.manifest = global::Newtonsoft.Json.JsonConvert.DeserializeObject<global::BountyBooster.Models.Manifest>(a);
				}
				catch (global::System.Exception ex)
				{
					global::BountyBooster.Logger.Output("Failed to deserialize manifest: " + ex.Message, "Error");
				}
			}
		}

		// Token: 0x02000011 RID: 17
		[global::HarmonyLib.HarmonyPatch(typeof(global::ServerBrowserList), "Refresh")]
		internal static class ServerBrowserRefreshPatch
		{
			// Token: 0x06000036 RID: 54 RVA: 0x00002DD8 File Offset: 0x00000FD8
			[global::HarmonyLib.HarmonyPrefix]
			public static bool Prefix(global::ServerBrowserList __instance)
			{
				if (__instance.name == "Official")
				{
					global::BountyBooster.BountyPlugin.browserList = __instance;
					__instance.categoryButton.gameObject.SetActive(false);
					__instance.showEmpty = true;
					return true;
				}
				global::BountyBooster.Patches.ManifestPatches.HideCategory(__instance);
				return false;
			}
		}

		// Token: 0x02000012 RID: 18
		[global::HarmonyLib.HarmonyPatch(typeof(global::ServerBrowserList), "ServerResponded")]
		internal static class ServerRespondedPatch
		{
			// Token: 0x06000037 RID: 55 RVA: 0x00002E14 File Offset: 0x00001014
			[global::HarmonyLib.HarmonyPrefix]
			public static bool Prefix(global::ServerBrowserList __instance, global::ServerInfo server)
			{
				if (__instance.name == "Official")
				{
					global::BountyBooster.BountyPlugin.browserList = __instance;
					global::BountyBooster.BountyPlugin.validAddress = server.Address.ToString() + ":" + server.ConnectionPort.ToString();
					__instance.showEmpty = true;
					__instance.categoryButton.Dirty();
					return true;
				}
				global::BountyBooster.Patches.ManifestPatches.HideCategory(__instance);
				return false;
			}
		}

		// Token: 0x02000013 RID: 19
		[global::HarmonyLib.HarmonyPatch(typeof(global::Steamworks.Data.ServerInfo), "From")]
		internal static class ServerSecurePatch
		{
			// Token: 0x06000038 RID: 56 RVA: 0x00002E7C File Offset: 0x0000107C
			[global::HarmonyLib.HarmonyPrefix]
			public static bool Prefix(global::Steamworks.Data.gameserveritem_t item)
			{
				item.Secure = true;
				return true;
			}
		}

		// Token: 0x02000014 RID: 20
		[global::HarmonyLib.HarmonyPatch(typeof(global::ConVar.Client), "connect")]
		public static class ConnectPatch
		{
			// Token: 0x06000039 RID: 57 RVA: 0x00002E86 File Offset: 0x00001086
			[global::HarmonyLib.HarmonyPrefix]
			public static bool Prefix(ref string address, string protocol)
			{
				if (global::BountyBooster.BountyPlugin.IsOfficialServer(address))
				{
					return true;
				}
				global::UnityEngine.Debug.Log((global::UnityEngine.Object)"Подключение только на BOUNTY RUST");
				return false;
			}
		}

		// Token: 0x02000015 RID: 21
		[global::HarmonyLib.HarmonyPatch(typeof(global::ServerBrowserList), "GetManifest")]
		public class GetManifest
		{
			// Token: 0x0600003A RID: 58 RVA: 0x00002EA8 File Offset: 0x000010A8
			public static void Postfix(global::ServerBrowserList __instance, string a)
			{
				try
				{
					global::BountyBooster.BountyPlugin.manifest = global::Newtonsoft.Json.JsonConvert.DeserializeObject<global::BountyBooster.Models.Manifest>(a);
				}
				catch (global::System.Exception ex)
				{
					global::BountyBooster.Logger.Output("Failed to GetManifest(): " + ex.Message, "Error");
				}
			}

			// Token: 0x0600003B RID: 59 RVA: 0x000020AE File Offset: 0x000002AE
			public GetManifest()
			{
			}
		}

		// Token: 0x02000016 RID: 22
		[global::HarmonyLib.HarmonyPatch(typeof(global::ServerBrowserList), "Awake")]
		public class Awake
		{
			// Token: 0x0600003C RID: 60 RVA: 0x00002EF0 File Offset: 0x000010F0
			public static void Postfix(global::ServerBrowserList __instance)
			{
				global::BountyBooster.BountyPlugin.browserList = __instance;
			}

			// Token: 0x0600003D RID: 61 RVA: 0x000020AE File Offset: 0x000002AE
			public Awake()
			{
			}
		}

		// Token: 0x02000017 RID: 23
		[global::HarmonyLib.HarmonyPatch(typeof(global::ServerBrowserList), "OnEnable")]
		public class OnEnable
		{
			// Token: 0x0600003E RID: 62 RVA: 0x00002EF0 File Offset: 0x000010F0
			public static void Postfix(global::ServerBrowserList __instance)
			{
				global::BountyBooster.BountyPlugin.browserList = __instance;
			}

			// Token: 0x0600003F RID: 63 RVA: 0x000020AE File Offset: 0x000002AE
			public OnEnable()
			{
			}
		}

		// Token: 0x02000018 RID: 24
		[global::HarmonyLib.HarmonyPatch(typeof(global::Client), "Connect")]
		public class Connect
		{
			// Token: 0x06000040 RID: 64 RVA: 0x00002EF8 File Offset: 0x000010F8
			public static void Prefix(string ipaddress, int port)
			{
				try
				{
					string text = ipaddress + ":" + port.ToString();
					if (global::BountyBooster.BountyPlugin.IsOfficialServer(text))
					{
						global::BountyBooster.Logger.Output("Connecting to official server: " + text, "");
					}
				}
				catch (global::System.Exception ex)
				{
					global::BountyBooster.Logger.Output("Failed to Connect(): " + ex.Message, "Error");
				}
			}

			// Token: 0x06000041 RID: 65 RVA: 0x000020AE File Offset: 0x000002AE
			public Connect()
			{
			}
		}
	}
}
