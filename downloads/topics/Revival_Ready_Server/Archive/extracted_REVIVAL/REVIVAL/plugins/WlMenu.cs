using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using WebSocketSharp;

namespace Oxide.Plugins
{
	[Info("WL Menu", "Mevent", "1.0.1")]
	public class WlMenu : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin ImageLibrary, WLReward;

		private const string Layer = "UI.Menu";

		#endregion

		#region Data

		// ЭТОТ ТЕКСТ НАСТРАИВАЕТСЯ - OXIDE/DATA
        List<string> MSGList = new List<string>
        {
            "<b>Ограничение игроков в команде - <color=blue><size=10>5 игроков</size></color></b>",
            "<b>Онлайн магазин - <color=blue><size=10>revivalrustshop.gamestores.app</size></color></b>",
            "<b>Множитель ресурсов - <color=blue><size=10>х1.5</size></color></b>", 
            "<b>Дискорд сервера - <color=blue><size=10>discord.gg/KFx5zs9V7W</size></color></b>",
            "<b>Группа вконтакте - <color=blue><size=10>vk.com/revivalrust</size></color></b>",
            "<b>Заметил подозрительного игрока? - пиши <color=blue><size=10>/report</size></color></b>"
			
        };

		#endregion

		#region Config

		private Configuration _config;

		private class Configuration
		{

            [JsonProperty("Интервал обновление сообщение")]
            public int intervalmsg = 7;

			[JsonProperty(PropertyName = "Title")] public string Title = "ДОБРО ПОЖАЛОВАТЬ НА REVIVAL RUST";

			[JsonProperty(PropertyName = "Logo Image")]
			public string Logo = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "WlMenu" + Path.DirectorySeparatorChar + "logo.png";

			[JsonProperty(PropertyName = "Background Image")]
			public string Background = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "WlMenu" + Path.DirectorySeparatorChar + "background.png";

			[JsonProperty(PropertyName = "Notify Image")]
			public string Notify = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "WlMenu" + Path.DirectorySeparatorChar + "notify.png";

			[JsonProperty(PropertyName = "Buttons", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<Button> Buttons = new List<Button>
			{
				new Button
				{
					Title = "НАБОРЫ",
					Command = "chat.say /kits",
					Image = string.Empty
				},
				new Button
				{
					Title = "КРАФТЫ",
					Command = "chat.say /craft",
					Image = string.Empty
				},
				new Button
				{
					Title = "ВАЙП БЛОК",
					Command = "chat.say /block",
					Image = string.Empty
				},
				new Button
				{
					Title = "ЖАЛОБЫ",
					Command = "chat.say /report",
					Image = string.Empty
				}
			};

			[JsonProperty(PropertyName = "Mini Buttons", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<Button> MiniButtons = new List<Button>
			{
				new Button
				{
					Title = string.Empty,
					Command = "chat.say /store",
					Image = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "WlMenu" + Path.DirectorySeparatorChar + "store.png"
				},
				new Button
				{
					Title = string.Empty,
					Command = "chat.say /info",
					Image = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "WlMenu" + Path.DirectorySeparatorChar + "timetomoney.png"
				}
			};
		}

		private class Button
		{
			[JsonProperty(PropertyName = "Title")] public string Title;

			[JsonProperty(PropertyName = "Image (for mini buttons)")]
			public string Image;

			[JsonProperty(PropertyName = "Command")]
			public string Command;

		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				_config = Config.ReadObject<Configuration>();
				if (_config == null) throw new Exception();
				SaveConfig();
			}
			catch
			{
				PrintError("Your configuration file contains an error. Using default configuration values.");
				LoadDefaultConfig();
			}
		}

		protected override void SaveConfig()
		{
			Config.WriteObject(_config);
		}

		protected override void LoadDefaultConfig()
		{
			_config = new Configuration();
		}

		#endregion

		#region Data

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

		#endregion

		#region Hooks

		private void OnServerInitialized()
		{
			LoadData();

			if (!ImageLibrary)
			{
				PrintWarning("IMAGE LIBRARY IS NOT INSTALLED.");
			}
			else
			{
				ImageLibrary.Call("AddImage", _config.Background, _config.Background);
				ImageLibrary.Call("AddImage", _config.Logo, _config.Logo);
				ImageLibrary.Call("AddImage", _config.Notify, _config.Notify);

				_config.MiniButtons.ForEach(btn =>
				{
					if (!btn.Image.IsNullOrEmpty())
						ImageLibrary.Call("AddImage", btn.Image, btn.Image);
				});
			}

            timer.Every(_config.intervalmsg, () =>
            {
				foreach(var player in BasePlayer.activePlayerList)
				    DrawLower(player);
            });


			foreach (var player in BasePlayer.activePlayerList)
				OnPlayerInit(player);

			AddCovalenceCommand("hide", nameof(CmdMenuHide));
		}

		private void Unload()
		{
			foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, Layer);

			SaveData();
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

		#endregion

		#region Commands

		private void CmdMenuHide(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			_data.ChangeStatus(player);

			MainUi(player);
		}

		#endregion

		#region Interface

		private void MainUi(BasePlayer player)
		{
			var container = new CuiElementContainer();

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 1", AnchorMax = "0 1"},
				Image = {Color = "0 0 0 0"}
			}, "Overlay", Layer);

			#region Main

			if (!_data.IsHided(player))
			{
				container.Add(new CuiElement
				{
					Name = Layer + ".Main",
					Parent = Layer,
					Components =
					{
						new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", _config.Background)},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "40.75 -64", OffsetMax = "435 -9.5"
						}
					}
				});

				#region Line

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 0",
						OffsetMin = "32 5",
						OffsetMax = "34 47"
					},
					Image = {Color = "1 1 1 1"}
				}, Layer + ".Main");

				#endregion

				#region Title

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 0",
						OffsetMin = "37.5 35",
						OffsetMax = "250 50"
					},
					Text =
					{
						Text = $"{_config.Title}",
						Align = TextAnchor.LowerLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					}
				}, Layer + ".Main");

				#endregion

				#region Online

				var SleepersPlayer = BasePlayer.sleepingPlayerList.Count;
	            string CargoPlaneCheck = IsCargoPlane() ? "007efe" : "007efe";
	            string BaseHelicopterCheck = IsBaseHelicopter() ? "007efe" : "007efe";

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 0",
						OffsetMin = "71.5 20",
						OffsetMax = "300 35"
					},
					Text =
					{
				Text = $"ОНЛАЙН: {ServerMgr.Instance.connectionQueue.Joining + BasePlayer.activePlayerList.Count}/{ConVar.Server.maxplayers} СПЯЩИЕ: {SleepersPlayer}",
						Align = TextAnchor.UpperLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 10,
						Color = "1 1 1 0.99"
					}
				}, Layer + ".Main", Layer + ".Online");

				#endregion

				#region Buttons

				var xSwitch = 37.5f;
				var Width = 60f;
				var Margin = 7f;

				for (var i = 0; i < _config.Buttons.Count; i++)
				{
					var button = _config.Buttons[i];

					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "0 0",
							OffsetMin = $"{xSwitch} 5",
							OffsetMax = $"{xSwitch + Width} 23"
						},
						Text =
						{
							Text = $"{button.Title}",
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 10,
							Color = "1 1 1 0.95"
						},
						Button =
						{
							Command = $"{button.Command}",
							Color = "0 0 0 0.60"
						}
					}, Layer + ".Main", Layer + $".BTN.{i}");

					if (i != _config.Buttons.Count - 1)
						container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "1 0", AnchorMax = "1 1",
								OffsetMin = "2.75 0",
								OffsetMax = "4.25 0"
							},
							Image = {Color = "1 1 1 1"}
						}, Layer + $".BTN.{i}");


					xSwitch += Width + Margin;
				}

				#endregion

				#region Mini Buttons

				xSwitch -= Margin;
				Width = 18f;
				Margin = 5f;

				for (var i = 0; i < _config.MiniButtons.Count; i++)
				{
					var button = _config.MiniButtons[i];

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "0 0",
							OffsetMin = $"{xSwitch - Width} 30",
							OffsetMax = $"{xSwitch} 48"
						},
						Image = {Color = "0 0 0 0.45"}
					}, Layer + ".Main", Layer + $".MiniButtons.{i}");

					if (!button.Image.IsNullOrEmpty())
						container.Add(new CuiElement
						{
							Parent = Layer + $".MiniButtons.{i}",
							Components =
							{
								new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", button.Image)},
								new CuiRectTransformComponent
								{
									AnchorMin = "0 0", AnchorMax = "1 1",
									OffsetMin = "2.5 2.5", OffsetMax = "-2.5 -2.5"
								}
							}
						});

					container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = ""},
						Button =
						{
							Color = "0 0 0 0",
							Command = $"{button.Command}"
						}
					}, Layer + $".MiniButtons.{i}");

					xSwitch = xSwitch - Width - Margin;
				}

				#endregion
			}

			#endregion

			#region Logo

			container.Add(new CuiElement
			{
				Name = Layer + ".Logo",
				Parent = Layer,
				Components =
				{
					new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", _config.Logo)},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 -65", OffsetMax = "70 -10"
					}
				}
			});

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = ""},
				Button =
				{
					Color = "0 0 0 0",
					Command = "hide"
				}
			}, Layer + ".Logo");

			#endregion

			CuiHelper.DestroyUi(player, Layer);
			CuiHelper.AddUi(player, container);

		}

		        bool IsCargoPlane()
		        {
		            foreach (var check in BaseNetworkable.serverEntities)
		                if (check is CargoPlane) return true;
		            return false;
		        }
		        bool IsBaseHelicopter()
		        {
		            foreach (var check in BaseNetworkable.serverEntities)
		                if (check is BaseHelicopter) return true;
		            return false;
		        }


		private void RefreshOnline()
		{
			var SleepersPlayer = BasePlayer.sleepingPlayerList.Count;
			string CargoPlaneCheck = IsCargoPlane() ? "007efe" : "007efe";
            string BaseHelicopterCheck = IsBaseHelicopter() ? "007efe" : "007efe";

			var container = new CuiElementContainer
			{
				{
					new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "71.5 20", OffsetMax = "300 35"
						},
						Text =
						{
							Text = $"ОНЛАЙН: {ServerMgr.Instance.connectionQueue.Joining + BasePlayer.activePlayerList.Count}/{ConVar.Server.maxplayers} СПЯЩИЕ: {SleepersPlayer}",
							Align = TextAnchor.UpperLeft,
							Font = "robotocondensed-bold.ttf",
							FontSize = 10,
							Color = "1 1 1 0.99"
						}
					},
					Layer + ".Main", Layer + ".Online"
				}
			};


			foreach (var player in BasePlayer.activePlayerList)
			{
				if (_data.IsHided(player)) continue;

				CuiHelper.DestroyUi(player, Layer + ".Online");
				CuiHelper.AddUi(player, container);
			}
		}


		private void DrawLower(BasePlayer player)
        {
			string MSGS = MSGList.GetRandom();

            string DVLower = "DVLower";
            CuiHelper.DestroyUi(player, DVLower);
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = HexToRustFormat("#00000000") },
                RectTransform = { AnchorMin = "0.3462664 0.003906242", AnchorMax = "0.6449488 0.02473958" },
                CursorEnabled = false,
            }, "Hud", DVLower);
			
            container.Add(new CuiElement
            {
                Parent = DVLower,
                Components =
                {
                    new CuiTextComponent { Text = MSGS, Align = TextAnchor.MiddleCenter, FontSize = 11, Font = "RobotoCondensed-regular.ttf"},
					new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.5 0.5" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            CuiHelper.AddUi(player, container);
        }

        private static string HexToRustFormat(string hex)
        {
            UnityEngine.Color color;
            ColorUtility.TryParseHtmlString(hex, out color);
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

		#endregion

	}
}