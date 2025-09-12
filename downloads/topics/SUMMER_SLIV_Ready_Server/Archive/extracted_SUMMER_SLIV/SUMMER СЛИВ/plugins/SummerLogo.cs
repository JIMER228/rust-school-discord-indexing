using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("SummerLogo", "Ryamkk", "2.0.0")]
	public class SummerLogo : RustPlugin
	{
		private static SummerLogo Instance;
		
		private GameObject FileManagerObject;
		private FileManager m_FileManager;

		private bool ImageInit = false;
		private const string Layer = "UI.Menu";
		
		List<string> MSGList = new List<string>
        {
    "Максимум человек в команде - <color=#ff405b>[ 3 ]</color>",
    "Наша группа ВК - <color=#ff405b>vk.com/summer_rust</color>",
    "Магазин/сайт сервера - <color=#ff405b>summer-store.xyz</color>",
    "<color=#ff405b>Delete</color> - открыть FPS BOOSTER",
    "Отобразить статистику игроков <color=#ff405b>/stat</color>",
    "<color=#ff405b>/report</color> - отправить жалобу на игрока"
        };

		private PluginData _data;

		private void SaveData()
		{
			Interface.Oxide.DataFileSystem.WriteObject(Name, _data);
		}

		private void LoadData()
		{
			try
			{
				_data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			if (_data == null) _data = new PluginData();
		}

		private class PluginData
		{
			[JsonProperty(PropertyName = "Hided Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ulong> HidedPlayers = new List<ulong>();

			public bool IsHided(BasePlayer player)
			{
				return HidedPlayers.Contains(player.userID);
			}

			public bool ChangeStatus(BasePlayer player)
			{
				if (player == null) return false;

				if (IsHided(player))
				{
					HidedPlayers.Remove(player.userID);
					return false;
				}

				HidedPlayers.Add(player.userID);
				return true;
			}
		}

		[JsonProperty("Картинки для графического интерфейса плагина")]
		private Dictionary<string, string> SummerLogoImageList = new Dictionary<string, string>()
		{
			["MainLogoV1"] = "https://cdn.discordapp.com/attachments/956598122560233492/1035880856960634920/MainLogo.png",
			["MainLogoV2"] = "https://cdn.discordapp.com/attachments/956598122560233492/1035886189774917713/MainLogoV2.png",
			["SLogo"]      = "https://cdn.discordapp.com/attachments/956598122560233492/1035880856570581032/SLogo.png"
		};
		
		void InitFileManager()
		{
			FileManagerObject = new GameObject("SummerLogo_FileManagerObject");
			m_FileManager = FileManagerObject.AddComponent<FileManager>();
		}

		private void OnServerInitialized()
		{
			Instance = this;
			
			InitFileManager();
			ServerMgr.Instance.StartCoroutine(LoadImages());
			
			LoadData();

			timer.Every(20f, () =>
            {
				foreach(var player in BasePlayer.activePlayerList)
				{
					DrawLower(player);
				}
            });


			foreach (var player in BasePlayer.activePlayerList)
			{
				OnPlayerInit(player);
			}

			AddCovalenceCommand("hide", nameof(CmdMenuHide));
		}
		
		IEnumerator LoadImages()
		{
			int i = 0;
			int lastpercent = -1;

			foreach (var name in SummerLogoImageList.Keys.ToList())
			{
				yield return m_FileManager.StartCoroutine(m_FileManager.LoadFile(name, SummerLogoImageList[name]));
				if (m_FileManager.GetPng(name) == null) yield return new WaitForSeconds(3);
				SummerLogoImageList[name] = m_FileManager.GetPng(name);
				int percent = (int) (i / (float) SummerLogoImageList.Keys.ToList().Count * 100);
				if (percent % 20 == 0 && percent != lastpercent) lastpercent = percent;

				i++;
			}

			ImageInit = true;
			m_FileManager.SaveData();
		}

		private void Unload()
		{
			foreach (var player in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(player, Layer);
			}
			
			SaveData();
		}

		void OnClientAuth(Connection connection)
		{
			foreach (var player in BasePlayer.activePlayerList)
			{
				timer.In(0.21f, RefreshOnline);
			}
		}

		private void OnPlayerInit(BasePlayer player)
		{
			if (player == null) return;

            if (player.IsReceivingSnapshot)
            {
                timer.Once(2, () => OnPlayerInit(player));
                return;
            }

			MainUi(player);
			DrawLower(player);
			RefreshOnline();
		}

		private void OnPlayerDisconnected(BasePlayer player)
		{
			timer.In(0.21f, RefreshOnline);
		}

		private void CmdMenuHide(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			_data.ChangeStatus(player);

			MainUi(player);
		}

		private void MainUi(BasePlayer player)
		{
			if (!ImageInit) return;
			
			var container = new CuiElementContainer();
			
			var SleepingPlayer = BasePlayer.sleepingPlayerList.Count;
			var OnlinePlayer = BasePlayer.activePlayerList.Count;
			var JoiningPlayer = ServerMgr.Instance.connectionQueue.Joining;
			
			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = "0 0 0 0" },
				RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -68", OffsetMax = "190 -1" }
			}, "Hud", Layer);

			if (!_data.IsHided(player))
			{
				container.Add(new CuiElement
				{
					Parent = Layer,
					Name = Layer + ".Image",
					FadeOut = 1f,
					Components =
					{
						new CuiRawImageComponent { FadeIn = 1f, Color = "1 1 1 1", Png = SummerLogoImageList["MainLogoV2"] },
						new CuiRectTransformComponent { AnchorMin = "0.01754384 0.05223882", AnchorMax = "0.9543861 0.9477612" }
					}
				});

				container.Add(new CuiLabel
				{
					RectTransform = { AnchorMin = "0.4421053 0.4378108", AnchorMax = "0.5473686 0.6268657" },
					Text = { Text = $"{JoiningPlayer + OnlinePlayer}", Color = HexToRustFormat("#D09537FF"), FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
				}, Layer, Layer + ".Online");
				
				container.Add(new CuiLabel
				{
					RectTransform = { AnchorMin = "0.8105242 0.4378108", AnchorMax = "0.915788 0.6268657" },
					Text = { Text = $"{SleepingPlayer}", FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
				}, Layer, Layer + ".Sleepers");
				
				container.Add(new CuiLabel
				{
					RectTransform = { AnchorMin = "0.6280694 0.4378108", AnchorMax = "0.7333332 0.6268657" },
					Text = { Text = $"{JoiningPlayer}", FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
				}, Layer, Layer + ".Joining");
				
				container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = "chat.say /top" },
                    RectTransform = { AnchorMin = "0.3824562 0.1194028", AnchorMax = "0.4666667 0.3582087" },
                    Text = { Text = "" }
                }, Layer, Layer + ".Image.1");
            
                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = "chat.say /store" },
                    RectTransform = { AnchorMin = "0.494737 0.1194028", AnchorMax = "0.5789481 0.3582087" },
                    Text = { Text = "" }
                }, Layer, Layer + ".Image.2");
            
                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = "chat.say /case" },
                    RectTransform = { AnchorMin = "0.6070169 0.1194028", AnchorMax = "0.6912282 0.3582087" },
                    Text = { Text = "" }
                }, Layer, Layer + ".Image.3");
            
                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = "chat.say /info" },
                    RectTransform = { AnchorMin = "0.7192968 0.1194028", AnchorMax = "0.803508 0.3582087" },
                    Text = { Text = "" }
                }, Layer, Layer + ".Image.4");
            
                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = "chat.say /menu" },
                    RectTransform = { AnchorMin = "0.8315767 0.1194028", AnchorMax = "0.915788 0.3582087" },
                    Text = { Text = "" }
                }, Layer, Layer + ".Image.5");
			}

			container.Add(new CuiElement
			{
				Parent = Layer,
				Name = Layer + ".SummerImage",
				Components =
				{
					new CuiRawImageComponent { Color = "1 1 1 1", Png = SummerLogoImageList["SLogo"] },
					new CuiRectTransformComponent { AnchorMin = "0.001864509 0.005549659", AnchorMax = "0.3913776 0.9055498" }
				}
			});

			container.Add(new CuiButton
			{
				RectTransform = { AnchorMin = "0 0", AnchorMax = "0.96 1" },
				Text = { Text = "" },
				Button = { Color = "0 0 0 0", Command = "hide" }
			}, Layer + ".SummerImage");

			CuiHelper.DestroyUi(player, Layer);
			CuiHelper.AddUi(player, container);

		}

		private void RefreshOnline()
		{
			var container = new CuiElementContainer();
				
			var SleepingPlayer = BasePlayer.sleepingPlayerList.Count;
			var OnlinePlayer = BasePlayer.activePlayerList.Count;
			var JoiningPlayer = ServerMgr.Instance.connectionQueue.Joining;

			container.Add(new CuiLabel
			{
				RectTransform = { AnchorMin = "0.4421053 0.4378108", AnchorMax = "0.5473686 0.6268657" },
				Text = { Text = $"{JoiningPlayer + OnlinePlayer}", Color = HexToRustFormat("#D09537FF"), FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
			}, Layer, Layer + ".Online");
				
			container.Add(new CuiLabel
			{
				RectTransform = { AnchorMin = "0.8105242 0.4378108", AnchorMax = "0.915788 0.6268657" },
				Text = { Text = $"{SleepingPlayer}", FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
			}, Layer, Layer + ".Sleepers");
				
			container.Add(new CuiLabel
			{
				RectTransform = { AnchorMin = "0.6280694 0.4378108", AnchorMax = "0.7333332 0.6268657" },
				Text = { Text = $"{JoiningPlayer}", FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
			}, Layer, Layer + ".Joining");

			foreach (var player in BasePlayer.activePlayerList)
			{
				if (_data.IsHided(player)) continue;

				CuiHelper.DestroyUi(player, Layer + ".Online");
				CuiHelper.DestroyUi(player, Layer + ".Sleepers");
				CuiHelper.DestroyUi(player, Layer + ".Joining");
				CuiHelper.AddUi(player, container);
			}
		}

		private void DrawLower(BasePlayer player)
        {
	        var container = new CuiElementContainer();
	        
            container.Add(new CuiPanel
            {
                Image = { Color = HexToRustFormat("#00000000") },
                RectTransform = { AnchorMin = "0.3462664 0.003906242", AnchorMax = "0.6449488 0.02473958" },
                CursorEnabled = false,
            }, "Under", "DVLower");
			
            container.Add(new CuiElement
            {
                Parent = "DVLower",
                Components =
                {
                    new CuiTextComponent { Text = MSGList.GetRandom(), Align = TextAnchor.MiddleCenter, FontSize = 11, Font = "RobotoCondensed-regular.ttf"},
					new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.5 0.5" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            CuiHelper.DestroyUi(player, "DVLower");
            CuiHelper.AddUi(player, container);
        }

        private static string HexToRustFormat(string hex)
        { 
	        Color color;
            ColorUtility.TryParseHtmlString(hex, out color);
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        class FileManager : MonoBehaviour
		{
			int loaded = 0;
			int needed = 0;

			Dictionary<string, FileInfo> files = new Dictionary<string, FileInfo>();
			DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile("SummerLogo/Images");

			private class FileInfo
			{
				public string Url;
				public string Png;
			}

			public void SaveData()
			{
				dataFile.WriteObject(files);
			}

			public void WipeData()
			{
				Interface.Oxide.DataFileSystem.WriteObject("SummerLogo/Images", new sbyte());
				Interface.Oxide.ReloadPlugin(Instance.Title);
			}

			public string GetPng(string name)
			{
				if (!files.ContainsKey(name)) return null;
				return files[name].Png;
			}

			private void Awake()
			{
				LoadData();
			}

			void LoadData()
			{
				try
				{
					files = dataFile.ReadObject<Dictionary<string, FileInfo>>();
				}
				catch
				{
					files = new Dictionary<string, FileInfo>();
				}
			}

			public IEnumerator LoadFile(string name, string url)
			{
				if (files.ContainsKey(name) && files[name].Url == url && !string.IsNullOrEmpty(files[name].Png))
					yield break;
				files[name] = new FileInfo() {Url = url};
				needed++;

				yield return StartCoroutine(LoadImageCoroutine(name, url));
			}

			IEnumerator LoadImageCoroutine(string name, string url)
			{
				using (WWW www = new WWW(url))
				{
					yield return www;
					{
						if (string.IsNullOrEmpty(www.error))
						{
							var entityId = CommunityEntity.ServerInstance.net.ID;
							var crc32 = FileStorage.server.Store(www.bytes, FileStorage.Type.png, entityId).ToString();
							files[name].Png = crc32;
						}
					}
				}

				loaded++;
			}
		}
	}
}