using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("InfoPanel", "Mevent#4546", "0.1.8")]
	public class InfoPanel : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin ImageLibrary, IQFakeActive;

		private const string Layer = "UI_InfoPanel";

		private readonly List<BasePlayer> MenuUsers = new List<BasePlayer>();

		private static InfoPanel instance;

		private readonly Dictionary<string, string> Events = new Dictionary<string, string>
		{
			["heli"] = "#FFFFFFFF",
			["air"] = "#FFFFFFFF",
			["cargo"] = "#FFFFFFFF",
			["bradley"] = "#FFFFFFFF",
			["ch47"] = "#FFFFFFFF"
		};

		#endregion

		#region Config

		private static ConfigData _config;

		private class ConfigData
		{
			[JsonProperty("Название сервера")] public ServerName ServerName = new ServerName
			{
				display = true,
				name = "<b>RUSTPLUGIN.RU</b>",
				OffMin = "43 -21",
				OffMax = "183 -5"
			};

			[JsonProperty("Тип отображения панели (Overlay или Hud)")]
			public string LayerType = "Overlay";

			[JsonProperty("Цвет для панелей (фон)")]
			public string LabelColor = "#A7A7A725";

			[JsonProperty("Цвет для панелей (закрытие)")]
			public string CloseColor = "#FF00003B";

			[JsonProperty(PropertyName = "Включить поддержку IQFakeActive?")]
			public bool UseIQFakeActive;

			[JsonProperty(PropertyName = "Команда для скрытия панели")]
			public string HideCmd = "panel";
			
			[JsonProperty("Настройка кнопки МЕНЮ")]
			public Menu menuCfg = new Menu
			{
				display = true,
				Title = "/MENU",
				Command = "menu",
				OffMin = "5 -55",
				OffMax = "40 -43"
			};

			[JsonProperty("Настройка игроков")] public Panel UsersIcon = new Panel
			{
				display = true,
				icon = "https://i.imgur.com/MUkpWFA.png",
				OffMin = "138 -40",
				OffMax = "183 -24"
			};

			[JsonProperty("Настройка времени")] public Panel TimeIcon = new Panel
			{
				display = true,
				icon = "https://i.imgur.com/c5AW7sO.png",
				OffMin = "186 -21",
				OffMax = "231 -5"
			};

			[JsonProperty("Настройка слиперов")] public Panel SleepersIcon = new Panel
			{
				display = true,
				icon = "https://i.imgur.com/UvLItA7.png",
				OffMin = "186 -40",
				OffMax = "231 -24"
			};

			[JsonProperty("Настройка координат")] public Panel CoordsPanel = new Panel
			{
				display = true,
				icon = "https://i.imgur.com/VicmD9Q.png",
				OffMin = "234 -21",
				OffMax = "344 -5"
			};

			[JsonProperty("Настройка логотипа")] public LogoSettings logosettings = new LogoSettings
			{
				display = true,
				icon = "https://i.imgur.com/UFmy9HT.png",
				LogoCmd = "chat.say /store",
				OffMin = "5 -40",
				OffMax = "40 -5"
			};

			[JsonProperty("Настройка экономики")] public Economy economy = new Economy
			{
				display = false,
				hook = "Balance",
				plugin = "Economics",
				icon = "https://i.imgur.com/K4dCGkQ.png",
				OffMin = "234 -40",
				OffMax = "294 -24"
			};

			[JsonProperty("Настройка иконок эвентов")]
			public SettingsEvents Events = new SettingsEvents
			{
				EventHelicopter = new EventSetting
				{
					display = true,
					icon = "https://i.imgur.com/Y0rVkt8.png",
					OnColor = "#0CF204FF",
					OffColor = "#FFFFFFFF",
					OffMin = "43 -40",
					OffMax = "59 -24"
				},
				EventAirdrop = new EventSetting
				{
					display = true,
					icon = "https://i.imgur.com/GcQKlg2.png",
					OnColor = "#0CF204FF",
					OffColor = "#FFFFFFFF",
					OffMin = "62 -40",
					OffMax = "78 -24"
				},
				EventCargoship = new EventSetting
				{
					display = true,
					icon = "https://i.imgur.com/3jigtJS.png",
					OnColor = "#0CF204FF",
					OffColor = "#FFFFFFFF",
					OffMin = "81 -40",
					OffMax = "97 -24"
				},
				EventBradley = new EventSetting
				{
					display = true,
					icon = "https://i.imgur.com/6Vtl3NG.png",
					OnColor = "#0CF204FF",
					OffColor = "#FFFFFFFF",
					OffMin = "100 -40",
					OffMax = "116 -24"
				},
				EventCh47 = new EventSetting
				{
					display = true,
					icon = "https://i.imgur.com/6U5ww9g.png",
					OnColor = "#0CF204FF",
					OffColor = "#FFFFFFFF",
					OffMin = "119 -40",
					OffMax = "135 -24"
				}
			};

			[JsonProperty("Кнопки меню")] public Buttons Btn = new Buttons
			{
				IndentStart = -58,
				btnHeight = 20,
				btnLenght = 130,
				btnMargin = 3,
				closeMenuBTN = new CloseMenuBTN
				{
					OffMin = "43 -55",
					OffMax = "53 -43"
				},
				btns = new List<Btn>
				{
					new Btn
					{
						URL = "https://i.imgur.com/WeHYCni.png",
						CMD = "chat.say /store",
						Title = "SHOP"
					},
					new Btn
					{
						URL = "https://i.imgur.com/buPPBW9.png",
						CMD = "chat.say /menu",
						Title = "MENU"
					},
					new Btn
					{
						URL = "https://i.imgur.com/oFhPHky.png",
						CMD = "chat.say /map",
						Title = "MAP"
					}
				}
			};
		}

		private class LogoSettings : Panel
		{
			[JsonProperty(PropertyName =
				"Команда [ПРИМЕР] чат команда: chat.say /store  ИЛИ  консольная команда: UI_GameStoresRUST")]
			public string LogoCmd;
		}

		private class Panel
		{
			[JsonProperty("Включить отображение?")]
			public bool display;

			[JsonProperty(PropertyName = "Ссылка на картинку")]
			public string icon;

			[JsonProperty(PropertyName = "Offset Min")]
			public string OffMin;

			[JsonProperty(PropertyName = "Offset Max")]
			public string OffMax;
		}

		private class Economy : Panel
		{
			[JsonProperty(PropertyName = "Вызываемая функция")]
			public string hook;

			[JsonProperty(PropertyName = "Название плагина")]
			public string plugin;

			public int ShowBalance(BasePlayer player)
			{
				return instance?.plugins?.Find(plugin)?.Call<int>(hook, player.userID) ?? 0;
			}
		}

		private class SettingsEvents
		{
			[JsonProperty(PropertyName = "Танк")] public EventSetting EventBradley;

			[JsonProperty(PropertyName = "Вертолёт")]
			public EventSetting EventHelicopter;

			[JsonProperty(PropertyName = "Самолёт")]
			public EventSetting EventAirdrop;

			[JsonProperty(PropertyName = "Корабль")]
			public EventSetting EventCargoship;

			[JsonProperty(PropertyName = "Чинук CH47")]
			public EventSetting EventCh47;
		}

		private class EventSetting : Panel
		{
			[JsonProperty(PropertyName = "Цвет активированного эвента")]
			public string OnColor;

			[JsonProperty(PropertyName = "Цвет дизактивированного эвента")]
			public string OffColor;
		}

		private class Buttons
		{
			[JsonProperty(PropertyName = "Отступ от края экрана")]
			public float IndentStart;

			[JsonProperty(PropertyName = "Длина кнопки")]
			public float btnLenght;

			[JsonProperty(PropertyName = "Высота кнопки")]
			public float btnHeight;

			[JsonProperty(PropertyName = "Отступ между кнопками")]
			public float btnMargin;

			[JsonProperty(PropertyName = "Кнопка закрытия меню")]
			public CloseMenuBTN closeMenuBTN;

			[JsonProperty(PropertyName = "Настройка кнопок", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<Btn> btns;
		}

		private class Btn
		{
			[JsonProperty(PropertyName = "Ссылка на картинку")]
			public string URL;

			[JsonProperty(PropertyName = "Команда")]
			public string CMD;

			[JsonProperty(PropertyName = "Название")]
			public string Title;
		}

		private class CloseMenuBTN
		{
			[JsonProperty(PropertyName = "Offset Min")]
			public string OffMin;

			[JsonProperty(PropertyName = "Offset Max")]
			public string OffMax;
		}

		private class Menu
		{
			[JsonProperty("Включить отображение?")]
			public bool display;

			[JsonProperty(PropertyName = "Название кнопки кнопки")]
			public string Title;

			[JsonProperty(PropertyName = "Команда кнопки")]
			public string Command;

			[JsonProperty(PropertyName = "Offset Min")]
			public string OffMin;

			[JsonProperty(PropertyName = "Offset Max")]
			public string OffMax;
		}

		private class ServerName
		{
			[JsonProperty("Включить отображение?")]
			public bool display;

			[JsonProperty(PropertyName = "Название")]
			public string name;

			[JsonProperty(PropertyName = "Offset Min")]
			public string OffMin;

			[JsonProperty(PropertyName = "Offset Max")]
			public string OffMax;
		}


		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				_config = Config.ReadObject<ConfigData>();
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
			_config = new ConfigData();
		}

		#endregion

		#region Data

		private PluginData _data;

		private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

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
			[JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ulong> Players = new List<ulong>();
		}

		#endregion
		
		#region Hooks

		private void OnServerInitialized()
		{
			if (!ImageLibrary)
			{
				PrintError("Please setup ImageLibrary plugin!");
				Interface.Oxide.UnloadPlugin(Title);
				return;
			}

			if (_config.economy.display && !plugins.Find(_config.economy.plugin))
			{
				PrintError("Please setup Economy plugin!");
				Interface.Oxide.UnloadPlugin(Title);
				return;
			}

			instance = this;

			LoadData();
			
			_config.Btn.btns.ForEach(btn => ImageLibrary.Call("AddImage", btn.URL, btn.URL));

			if (_config.logosettings.display)
				ImageLibrary.Call("AddImage", _config.logosettings.icon, _config.logosettings.icon);
			if (_config.UsersIcon.display)
				ImageLibrary.Call("AddImage", _config.UsersIcon.icon, _config.UsersIcon.icon);
			if (_config.TimeIcon.display)
				ImageLibrary.Call("AddImage", _config.TimeIcon.icon, _config.TimeIcon.icon);
			if (_config.SleepersIcon.display)
				ImageLibrary.Call("AddImage", _config.SleepersIcon.icon, _config.SleepersIcon.icon);
			if (_config.CoordsPanel.display)
				ImageLibrary.Call("AddImage", _config.CoordsPanel.icon, _config.CoordsPanel.icon);
			if (_config.Events.EventAirdrop.display)
				ImageLibrary.Call("AddImage", _config.Events.EventAirdrop.icon,
					_config.Events.EventAirdrop.icon);
			if (_config.Events.EventBradley.display)
				ImageLibrary.Call("AddImage", _config.Events.EventBradley.icon,
					_config.Events.EventBradley.icon);
			if (_config.Events.EventCargoship.display)
				ImageLibrary.Call("AddImage", _config.Events.EventCargoship.icon,
					_config.Events.EventCargoship.icon);
			if (_config.Events.EventHelicopter.display)
				ImageLibrary.Call("AddImage", _config.Events.EventHelicopter.icon,
					_config.Events.EventHelicopter.icon);
			if (_config.Events.EventCh47.display)
				ImageLibrary.Call("AddImage", _config.Events.EventCh47.icon,
					_config.Events.EventCh47.icon);
			if (_config.economy.display)
				ImageLibrary.Call("AddImage", _config.economy.icon, _config.economy.icon);

			foreach (var entity in BaseNetworkable.serverEntities)
				OnEntitySpawned(entity as BaseEntity);

			foreach (var player in BasePlayer.activePlayerList)
				InitializeUI(player);

			if (_config.TimeIcon.display ||
			    _config.economy.display ||
			    _config.CoordsPanel.display)
				timer.Every(5, () =>
				{
					foreach (var player in BasePlayer.activePlayerList)
					{
						if (player.IsNpc || player.IsSleeping() || player.IsReceivingSnapshot) continue;

						if (_config.TimeIcon.display) RefreshUI(player, "time");
						if (_config.economy.display) RefreshUI(player, "balance");
						if (_config.CoordsPanel.display) RefreshUI(player, "coords");
					}
				});

			AddCovalenceCommand(_config.menuCfg.Command, nameof(CmdMenu));
			AddCovalenceCommand(_config.HideCmd, nameof(CmdSwitch));
		}

		private void Unload()
		{
			foreach (var player in BasePlayer.activePlayerList)
				CuiHelper.DestroyUi(player, Layer);

			SaveData();
			
			_config = null;
			instance = null;
		}

		private void OnEntitySpawned(BaseEntity entity)
		{
			EntityHandle(entity, true);
		}

		private void OnEntityKill(BaseEntity entity)
		{
			EntityHandle(entity, false);
		}

		private void OnPlayerConnected(BasePlayer player)
		{
			if (player.IsReceivingSnapshot || player.IsSleeping())
			{
				timer.In(1, () => OnPlayerConnected(player));
				return;
			}

			InitializeUI(player);

			UpdateOnline();
		}

		private void OnPlayerDisconnected(BasePlayer player, string reason)
		{
			timer.In(1f, UpdateOnline);
		}

		#endregion

		#region Commands

		private void CmdMenu(IPlayer user, string cmd, string[] args)
		{
			var player = user?.Object as BasePlayer;
			if (player == null) return;

			if (MenuUsers.Contains(player))
			{
				CuiHelper.DestroyUi(player, Layer + ".Menu.Opened");
				MenuUsers.Remove(player);
			}
			else
			{
				ButtonsUI(player);
				MenuUsers.Add(player);
			}
		}

		[ConsoleCommand("sendconscmd")]
		private void SendCMD(ConsoleSystem.Arg args)
		{
			if (args.Player() != null)
			{
				var player = args.Player();
				var convertcmd =
					$"{args.Args[0]}  \" {string.Join(" ", args.Args.ToList().GetRange(1, args.Args.Length - 1))}\" 0";
				player.SendConsoleCommand(convertcmd);
			}
		}
		
		private void CmdSwitch(IPlayer cov, string cmd, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (args.Length == 0)
			{
				if (_data.Players.Contains(player.userID))
				{
					_data.Players.Remove(player.userID);
					InitializeUI(player);
				}
				else
				{
					_data.Players.Add(player.userID);
					CuiHelper.DestroyUi(player, Layer);
				}
				
				return;
			}

			switch (args[0])
			{
				case "show":
				case "on":
				{
					if (_data.Players.Remove(player.userID)) 
						InitializeUI(player);
					break;
				}
				case "hide":
				case "off":
				{
					if (!_data.Players.Contains(player.userID))
						_data.Players.Add(player.userID);
					CuiHelper.DestroyUi(player, Layer);
					break;
				}
			}
		}

		#endregion

		#region Interface

		private void InitializeUI(BasePlayer player)
		{
			if (_data.Players.Contains(player.userID)) return;
			
			var container = new CuiElementContainer
			{
				{
					new CuiPanel {RectTransform = {AnchorMin = "0 1", AnchorMax = "0 1"}, Image = {Color = "0 0 0 0"}},
					_config.LayerType, Layer
				}
			};


			if (_config.logosettings.display)
			{
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = _config.logosettings.OffMin,
						OffsetMax = _config.logosettings.OffMax
					},
					Button = {Color = HexToCuiColor(_config.LabelColor), Command = _config.logosettings.LogoCmd},
					Text = {Text = ""}
				}, Layer, Layer + ".Logo");
				UI.LoadImage(ref container, ".Logo.Icon", ".Logo", oMin: "2 2", oMax: "-2 -2",
					image: _config.logosettings.icon);
			}

			if (_config.TimeIcon.display)
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = _config.TimeIcon.OffMin,
						OffsetMax = _config.TimeIcon.OffMax
					},
					Image = {Color = HexToCuiColor(_config.LabelColor)}
				}, Layer, Layer + ".Time.Label");
				UI.LoadImage(ref container, ".Time.Icon", ".Time.Label", "0 0", "0 1", "1 1",
					"13 -1", image: _config.TimeIcon.icon);
			}

			if (_config.SleepersIcon.display)
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = _config.SleepersIcon.OffMin,
						OffsetMax = _config.SleepersIcon.OffMax
					},
					Image = {Color = HexToCuiColor(_config.LabelColor)}
				}, Layer, Layer + ".Sleepers.Label");
				UI.LoadImage(ref container, ".Sleepers.Icon", ".Sleepers.Label", "0 0", "0 1", "1 1",
					"13 -1", image: _config.SleepersIcon.icon);
			}

			if (_config.CoordsPanel.display)
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = _config.CoordsPanel.OffMin,
						OffsetMax = _config.CoordsPanel.OffMax
					},
					Image = {Color = HexToCuiColor(_config.LabelColor)}
				}, Layer, Layer + ".Coords.Label");
				UI.LoadImage(ref container, ".Coords.Icon", ".Coords.Label", "0 0", "0 1", "1 1",
					"13 -1", image: _config.CoordsPanel.icon);
			}

			if (_config.ServerName.display)
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = _config.ServerName.OffMin,
						OffsetMax = _config.ServerName.OffMax
					},
					Image = {Color = HexToCuiColor(_config.LabelColor)}
				}, Layer, Layer + ".ServerName");
				container.Add(new CuiLabel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text =
					{
						FadeIn = 1f, Color = "1 1 1 1", Text = _config.ServerName.name,
						Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12
					}
				}, Layer + ".ServerName");
			}

			if (_config.Events.EventHelicopter.display)
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = _config.Events.EventHelicopter.OffMin,
						OffsetMax = _config.Events.EventHelicopter.OffMax
					},
					Image = {Color = HexToCuiColor(_config.LabelColor)}
				}, Layer, Layer + ".Helicopter");
			if (_config.Events.EventAirdrop.display)
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = _config.Events.EventAirdrop.OffMin,
						OffsetMax = _config.Events.EventAirdrop.OffMax
					},
					Image = {Color = HexToCuiColor(_config.LabelColor)}
				}, Layer, Layer + ".Air");
			if (_config.Events.EventCargoship.display)
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = _config.Events.EventCargoship.OffMin,
						OffsetMax = _config.Events.EventCargoship.OffMax
					},
					Image = {Color = HexToCuiColor(_config.LabelColor)}
				}, Layer, Layer + ".Cargo");
			if (_config.Events.EventBradley.display)
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = _config.Events.EventBradley.OffMin,
						OffsetMax = _config.Events.EventBradley.OffMax
					},
					Image = {Color = HexToCuiColor(_config.LabelColor)}
				}, Layer, Layer + ".Bradley");
			if (_config.Events.EventCh47.display)
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = _config.Events.EventCh47.OffMin,
						OffsetMax = _config.Events.EventCh47.OffMax
					},
					Image = {Color = HexToCuiColor(_config.LabelColor)}
				}, Layer, Layer + ".CH47");
			if (_config.UsersIcon.display)
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = _config.UsersIcon.OffMin,
						OffsetMax = _config.UsersIcon.OffMax
					},
					Image = {Color = HexToCuiColor(_config.LabelColor)}
				}, Layer, Layer + ".Online.Label");
				UI.LoadImage(ref container, ".Online.Icon", ".Online.Label", "0 0", "0 1", "1 1",
					"13 -1", image: _config.UsersIcon.icon);
			}

			if (_config.economy.display)
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = _config.economy.OffMin,
						OffsetMax = _config.economy.OffMax
					},
					Image = {Color = HexToCuiColor(_config.LabelColor)}
				}, Layer, Layer + ".Balance.Label");
				UI.LoadImage(ref container, ".Balance.Icon", ".Balance.Label", "0 0", "0 1", "1 1",
					"14 -1", image: _config.economy.icon);
			}

			if (_config.menuCfg.display)
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = _config.menuCfg.OffMin,
						OffsetMax = _config.menuCfg.OffMax
					},
					Button = {Color = HexToCuiColor(_config.LabelColor), Command = "chat.say /menu"},
					Text =
					{
						Color = "1 1 1 1", Text = _config.menuCfg.Title, Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf", FontSize = 10
					}
				}, Layer, Layer + ".Menu");

			CuiHelper.DestroyUi(player, Layer);
			CuiHelper.AddUi(player, container);

			RefreshUI(player, "all");
		}

		private void ButtonsUI(BasePlayer player)
		{
			if (_data.Players.Contains(player.userID)) return;

			var ButtonsContainer = new CuiElementContainer();
			var ySwitch = _config.Btn.IndentStart;

			ButtonsContainer.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image = {Color = "0 0 0 0"}
			}, Layer, Layer + ".Menu.Opened");

			ButtonsContainer.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = _config.Btn.closeMenuBTN.OffMin,
					OffsetMax = _config.Btn.closeMenuBTN.OffMax
				},
				Image = {Color = HexToCuiColor(_config.CloseColor)}
			}, Layer + ".Menu.Opened", Layer + ".Menu.Opened.Close");
			ButtonsContainer.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Button = {Color = "0 0 0 0", Command = $"chat.say /{_config.menuCfg.Command}"},
				Text =
				{
					Text = "X", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 10
				}
			}, Layer + ".Menu.Opened.Close");

			_config.Btn.btns.ForEach(button =>
			{
				ButtonsContainer.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"5 {ySwitch - _config.Btn.btnHeight}",
						OffsetMax = $"{_config.Btn.btnLenght + 5} {ySwitch}"
					},
					Image = {Color = HexToCuiColor(_config.LabelColor)}
				}, Layer + ".Menu.Opened", Layer + $".Menu.Opened.{button.URL}");
				ySwitch -= _config.Btn.btnHeight + _config.Btn.btnMargin;
				UI.LoadImage(ref ButtonsContainer, $".Menu.Opened.{button.URL}.Img", $".Menu.Opened.{button.URL}",
					"0 0", "0 1", "3 1", "21 -1", image: button.URL);
				ButtonsContainer.Add(new CuiLabel
				{
					RectTransform =
						{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"{_config.Btn.btnHeight + 2} 0"},
					Text =
					{
						Text = $"{button.Title}", Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf", FontSize = 12
					}
				}, Layer + $".Menu.Opened.{button.URL}");

				ButtonsContainer.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Button = {Color = "0 0 0 0", Command = $"sendconscmd {button.CMD}"},
					Text = {Text = ""}
				}, Layer + $".Menu.Opened.{button.URL}");
			});

			CuiHelper.DestroyUi(player, Layer + ".Menu.Opened");
			CuiHelper.AddUi(player, ButtonsContainer);
		}

		private void RefreshUI(BasePlayer player, string Type)
		{
			if (_data.Players.Contains(player.userID)) return;

			var RefreshContainer = new CuiElementContainer();
			switch (Type)
			{
				case "coords":
					var position = player.transform.position;
					UI.CreateLabel(ref RefreshContainer, player, ".Refresh.Coords", ".Coords.Label",
						text:
						$"X: {position.x:0} Z: {position.z:0}");
					break;
				case "online":
					UI.CreateLabel(ref RefreshContainer, player, ".Refresh.Online", ".Online.Label",
						text: $"{GetOnline()}");
					break;
				case "balance":
					UI.CreateLabel(ref RefreshContainer, player, ".Refresh.Balance", ".Balance.Label",
						oMin: "14 1",
						text: $"{_config.economy.ShowBalance(player)}");
					break;
				case "time":
					UI.CreateLabel(ref RefreshContainer, player, ".Refresh.Time", ".Time.Label",
						text: TOD_Sky.Instance.Cycle.DateTime.ToString("HH:mm"));
					break;
				case "sleepers":
					UI.CreateLabel(ref RefreshContainer, player, ".Refresh.Sleepers", ".Sleepers.Label",
						text: $"{BasePlayer.sleepingPlayerList.Count}");
					break;
				case "heli":
					CuiHelper.DestroyUi(player, Layer + ".Events.Helicopter");
					UI.LoadImage(ref RefreshContainer, ".Events.Helicopter", ".Helicopter", oMin: "1 1", oMax: "-1 -1",
						color: HexToCuiColor(Events[Type]), image: _config.Events.EventHelicopter.icon);
					break;
				case "air":
					CuiHelper.DestroyUi(player, Layer + ".Events.Air");
					UI.LoadImage(ref RefreshContainer, ".Events.Air", ".Air", oMin: "1 1", oMax: "-1 -1",
						color: HexToCuiColor(Events[Type]), image: _config.Events.EventAirdrop.icon);
					break;
				case "cargo":
					CuiHelper.DestroyUi(player, Layer + ".Events.Cargo");
					UI.LoadImage(ref RefreshContainer, ".Events.Cargo", ".Cargo", oMin: "1 1", oMax: "-1 -1",
						color: HexToCuiColor(Events[Type]), image: _config.Events.EventCargoship.icon);
					break;
				case "bradley":
					CuiHelper.DestroyUi(player, Layer + ".Events.Bradley");
					UI.LoadImage(ref RefreshContainer, ".Events.Bradley", ".Bradley", oMin: "1 1", oMax: "-1 -1",
						color: HexToCuiColor(Events[Type]), image: _config.Events.EventBradley.icon);
					break;
				case "ch47":
					CuiHelper.DestroyUi(player, Layer + ".Events.CH47");
					UI.LoadImage(ref RefreshContainer, ".Events.CH47", ".CH47", oMin: "1 1", oMax: "-1 -1",
						color: HexToCuiColor(Events[Type]), image: _config.Events.EventCh47.icon);
					break;
				case "all":
					CuiHelper.DestroyUi(player, Layer + ".Events.Helicopter");
					CuiHelper.DestroyUi(player, Layer + ".Events.Air");
					CuiHelper.DestroyUi(player, Layer + ".Events.Cargo");
					CuiHelper.DestroyUi(player, Layer + ".Events.Bradley");
					CuiHelper.DestroyUi(player, Layer + ".Events.CH47");
					if (_config.CoordsPanel.display)
					{
						var position1 = player.transform.position;
						UI.CreateLabel(ref RefreshContainer, player, ".Refresh.Coords", ".Coords.Label",
							text:
							$"X: {position1.x:0} Z: {position1.z:0}");
					}

					if (_config.UsersIcon.display)
						UI.CreateLabel(ref RefreshContainer, player, ".Refresh.Online", ".Online.Label",
							text: $"{GetOnline()}");
					if (_config.economy.display)
						UI.CreateLabel(ref RefreshContainer, player, ".Refresh.Balance", ".Balance.Label",
							oMin: "14 1",
							text: $"{_config.economy.ShowBalance(player)}");
					if (_config.TimeIcon.display)
						UI.CreateLabel(ref RefreshContainer, player, ".Refresh.Time", ".Time.Label",
							text: TOD_Sky.Instance.Cycle.DateTime.ToString("HH:mm"));
					if (_config.SleepersIcon.display)
						UI.CreateLabel(ref RefreshContainer, player, ".Refresh.Sleepers",
							".Sleepers.Label", text: $"{BasePlayer.sleepingPlayerList.Count}");
					if (_config.Events.EventHelicopter.display)
						UI.LoadImage(ref RefreshContainer, ".Events.Helicopter", ".Helicopter", oMin: "1 1",
							oMax: "-1 -1", color: HexToCuiColor(Events["heli"]),
							image: _config.Events.EventHelicopter.icon);
					if (_config.Events.EventAirdrop.display)
						UI.LoadImage(ref RefreshContainer, ".Events.Air", ".Air", oMin: "1 1", oMax: "-1 -1",
							color: HexToCuiColor(Events["air"]), image: _config.Events.EventAirdrop.icon);
					if (_config.Events.EventCargoship.display)
						UI.LoadImage(ref RefreshContainer, ".Events.Cargo", ".Cargo", oMin: "1 1", oMax: "-1 -1",
							color: HexToCuiColor(Events["cargo"]), image: _config.Events.EventCargoship.icon);
					if (_config.Events.EventBradley.display)
						UI.LoadImage(ref RefreshContainer, ".Events.Bradley", ".Bradley", oMin: "1 1", oMax: "-1 -1",
							color: HexToCuiColor(Events["bradley"]), image: _config.Events.EventBradley.icon);
					if (_config.Events.EventCh47.display)
						UI.LoadImage(ref RefreshContainer, ".Events.CH47", ".CH47", oMin: "1 1", oMax: "-1 -1",
							color: HexToCuiColor(Events["ch47"]), image: _config.Events.EventCh47.icon);
					break;
			}

			CuiHelper.AddUi(player, RefreshContainer);
		}

		#endregion

		#region Utils

		private void UpdateOnline()
		{
			foreach (var basePlayer in BasePlayer.activePlayerList)
			{
				if (_config.UsersIcon.display) RefreshUI(basePlayer, "online");
				if (_config.SleepersIcon.display) RefreshUI(basePlayer, "sleepers");
			}
		}

		private class UI
		{
			public static void LoadImage(ref CuiElementContainer container, string name, string parent,
				string aMin = "0 0", string aMax = "1 1", string oMin = "13 1", string oMax = "0 -1",
				string color = "1 1 1 1", string image = "")
			{
				container.Add(new CuiElement
				{
					Name = Layer + name,
					Parent = Layer + parent,
					Components =
					{
						new CuiRawImageComponent
							{Png = instance.ImageLibrary.Call<string>("GetImage", $"{image}"), Color = color},
						new CuiRectTransformComponent
							{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
					}
				});
			}

			public static void CreateLabel(ref CuiElementContainer container, BasePlayer player, string name,
				string parent, string aMin = "0 0", string aMax = "1 1", string oMin = "13 1", string oMax = "0 -1",
				string color = "1 1 1 1", string text = "", TextAnchor align = TextAnchor.MiddleCenter,
				int fontsize = 12, string font = "robotocondensed-regular.ttf")
			{
				CuiHelper.DestroyUi(player, Layer + name);
				container.Add(new CuiLabel
				{
					RectTransform = {AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax},
					Text = {Text = text, Color = color, Align = align, Font = font, FontSize = fontsize}
				}, Layer + parent, Layer + name);
			}
		}

		private void EntityHandle(BaseEntity entity, bool spawn)
		{
			if (entity == null) return;

			if (entity is CargoPlane && _config.Events.EventAirdrop.display)
			{
				Events["air"] = spawn ? _config.Events.EventAirdrop.OnColor : _config.Events.EventAirdrop.OffColor;
				foreach (var player in BasePlayer.activePlayerList)
					RefreshUI(player, "air");
			}

			if (entity is BradleyAPC && _config.Events.EventBradley.display)
			{
				Events["bradley"] = spawn ? _config.Events.EventBradley.OnColor : _config.Events.EventBradley.OffColor;
				foreach (var player in BasePlayer.activePlayerList)
					RefreshUI(player, "bradley");
			}

			if (entity is BaseHelicopter && _config.Events.EventHelicopter.display)
			{
				Events["heli"] =
					spawn ? _config.Events.EventHelicopter.OnColor : _config.Events.EventHelicopter.OffColor;
				foreach (var player in BasePlayer.activePlayerList)
					RefreshUI(player, "heli");
			}

			if (entity is CargoShip && _config.Events.EventCargoship.display)
			{
				Events["cargo"] =
					spawn ? _config.Events.EventCargoship.OnColor : _config.Events.EventCargoship.OffColor;
				foreach (var player in BasePlayer.activePlayerList)
					RefreshUI(player, "cargo");
			}

			if (entity is CH47Helicopter && _config.Events.EventCh47.display)
			{
				Events["ch47"] = spawn ? _config.Events.EventCh47.OnColor : _config.Events.EventCh47.OffColor;
				foreach (var player in BasePlayer.activePlayerList)
					RefreshUI(player, "ch47");
			}
		}

		private static string HexToCuiColor(string hex)
		{
			if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";

			var str = hex.Trim('#');

			if (str.Length == 6)
				str += "FF";

			if (str.Length != 8) throw new Exception(hex);

			var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
			var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
			var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
			var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

			Color color = new Color32(r, g, b, a);

			return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
		}

		private int GetOnline()
		{
			return _config.UseIQFakeActive
				? IQFakeActive?.Call<int>("GetOnline") ?? BasePlayer.activePlayerList.Count
				: BasePlayer.activePlayerList.Count;
		}

		#endregion

		#region IQFakeActive

		private void SyncReservedFinish()
		{
			if (!_config.UseIQFakeActive) return;
			PrintWarning("InfoPanel - успешно синхронизирована с IQFakeActive");
			PrintWarning("=============SYNC==================");
		}

		#endregion
	}
}