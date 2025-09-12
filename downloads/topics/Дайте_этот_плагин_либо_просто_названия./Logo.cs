using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("GloryLg", "B", "1.0.0")]
	public class Logo : RustPlugin
	{
		[PluginReference] private Plugin ImageLibrary;

		public string Layer = "GloryLg";

		public Dictionary<string, string> Image = new Dictionary<string, string>()
		{
			["Menu"] = "https://i.postimg.cc/fW2n6NMy/image.png",

			["SleepOnline"] = "https://i.postimg.cc/P5B2D6Td/image.png",
		};
		

		private string GetImage(string fileName, ulong skin = 0)
		{
			var imageId = (string)plugins.Find("ImageLibrary").CallHook("GetImage", fileName, skin);
			if (!string.IsNullOrEmpty(imageId))
				return imageId;
			return string.Empty;
		}

		public bool AddImage(string url, string shortname, ulong skin = 0)
		{
			return (bool) ImageLibrary?.Call("AddImage", url, shortname, skin);
		}

		void LoadImage()
		{
			foreach (var image in Image)
			{
				AddImage(image.Value, image.Key);
			}
		}

		void OnServerInitialized()
		{
			LoadImage();

			foreach (var player in BasePlayer.activePlayerList)
			{
				OnPlayerConnected(player);

				foreach (var entity in BaseNetworkable.serverEntities)
				{
					if (entity == null || entity?.net.ID == null) return;
					try
					{
						NextTick(() =>
						{

							if (entity is BradleyAPC)
							{
								RefreshUI(player, "BradleyAPC");
							}

							if (entity is CH47Helicopter)
							{
								RefreshUI(player, "CH47Helicopter");
							}
						});
					}
					catch (NullReferenceException) { }
				}
			}
		}

		void OnPlayerConnected(BasePlayer player)
		{
			if (player == null) return;
			
			LogoUI(player);
			
			foreach (var players in BasePlayer.activePlayerList)
			{
				if (players == null) return;
				
				NextTick(() => { RefreshUI(players, "Online"); });
			}
		}
		
		void OnPlayerDisconnected(BasePlayer player, string reason)
		{
			if (player == null) return;
			
			NextTick(() =>
			{
				foreach (var players in BasePlayer.activePlayerList)
				{
					if (players == null) return;
					
					RefreshUI(players, "Online");
				}
			});
		}
		
		void Unload()
		{
			foreach (BasePlayer player in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(player, Layer);
			}
		}
		

		void LogoUI(BasePlayer player)
		{
			if(player == null) return;
			
			CuiElementContainer container = new CuiElementContainer();

			#region UI elements
			
			int OnlinePlayer = BasePlayer.activePlayerList.Count;
			int SleepingPlayer = BasePlayer.sleepingPlayerList.Count;
			float FadeIn = 0.1f;
			float FadeOut = 0.1f;
			
			#endregion

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = "0 0 0 0" },
				RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -85", OffsetMax = "200 -1" }
			}, "Hud", Layer);

			container.Add(new CuiElement
			{
				Parent = Layer,
				Name = Layer + ".Menu",
				FadeOut = 1f,
				Components =
				{
					new CuiImageComponent { Png = GetImage("Menu"), Material = "assets/icons/greyout.mat" },
					new CuiRectTransformComponent { AnchorMin = "0.01116699 0.284884", AnchorMax = "0.2925 0.9761907" }
				}
			});
			container.Add(new CuiElement
			{
				Parent = Layer,
				Name = Layer + ".Online",
				FadeOut = 1f,
				Components =
				{
					new CuiImageComponent { Png = GetImage("SleepOnline"), Material = "assets/icons/greyout.mat" },
					new CuiRectTransformComponent { AnchorMin = "0.3111669 0.3895351", AnchorMax = "0.48 0.8947953" }
				}
			});

			container.Add(new CuiButton
			{
				Button = { Color = "0 0 0 0", Command = $"chat.say /store" },
				RectTransform = { AnchorMin = "-0.181333 0.1904763", AnchorMax = "0.6319999 0.9761907" },
				Text = { Text = "" }
			}, Layer, Layer + ".Menu");
			container.Add(new CuiLabel
			{
				Text =
				{
					Text = $"{OnlinePlayer}",
					FontSize = 10,
					Align = TextAnchor.MiddleCenter,
					Color = "1 1 1 1" 
				},
				RectTransform =
				{
					AnchorMin = "0.3511669 0.6495351",
					AnchorMax = "0.48 0.8947953"
				}
			}, Layer, Layer + ".Online");
			container.Add(new CuiLabel
			{
				Text =
				{
					Text = $"{SleepingPlayer}",
					FontSize = 10,
					Align = TextAnchor.MiddleCenter,
					Color = "1 1 1 1" 
				},
				RectTransform =
				{
					AnchorMin = "0.3511669 0.1495351",
					AnchorMax = "0.48 0.8947953"
				}
			}, Layer, Layer + ".Sleepers");
			CuiHelper.DestroyUi(player, Layer);
			CuiHelper.AddUi(player, container);
		}

		void RefreshUI(BasePlayer player, string Type)
		{
			CuiElementContainer RefreshContainer = new CuiElementContainer();
			
			int OnlinePlayer = BasePlayer.activePlayerList.Count;
			int SleepingPlayer = BasePlayer.sleepingPlayerList.Count;
			float FadeIn = 0.1f;
			float FadeOut = 0.1f;
			
			CuiHelper.AddUi(player, RefreshContainer);
		}
		
		public static string Color(string hexColor, float alpha = 1)
		{
			if (hexColor.StartsWith("#"))
				hexColor = hexColor.Substring(1);
			var red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
			var green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
			var blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
			return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
		}
	}
}