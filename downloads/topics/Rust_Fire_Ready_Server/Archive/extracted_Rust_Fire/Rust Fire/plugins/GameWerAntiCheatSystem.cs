//Reference: I18N
//Reference: I18N.Other

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using I18N.Other;
using Network;
using Newtonsoft.Json;
using Oxide.Core.Libraries;
using WebSocketSharp;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("GameWer AntiCheat System", "TheRyuzaki & rostov114", "3.8.1")]
	class GameWerAntiCheatSystem : RustPlugin
	{
		#region Variables

		private static GameWerAntiCheatSystem _instance;
		private Configuration _config;
		private WSH _wsh;
		private bool _hasUnloaded = false;
		private List<Connection> _authList = Pool.GetList<Connection>();
		private Dictionary<ulong, string> _machineIDList = new Dictionary<ulong, string>();

		#endregion

		#region Language

		private void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["gamewer-use-vpn"] = "Inconsistency of IP came to us and was detected in anti-cheat!",
				["gamewer-join_for_outher_server"] = "Another server reported that you went to them.",
				["anticheat-offline"] = "Download and run GameWer anti-cheat - http://vk.com/allowerd",
				["gamewer-alreadyDetected"] = "Something went wrong, restart the anti-cheat!",
				["gamewer-closed"] = "Communication with anti-cheat is on your side - lost, restart anti-cheat!",
			}, this, "en");

			lang.RegisterMessages(new Dictionary<string, string>
			{
				["gamewer-use-vpn"] = "Обнаружено несоотвествение IP пришедшего к нам и в античит!",
				["gamewer-join_for_outher_server"] = "Другой сервер сообщил, что вы зашли к ним.",
				["anticheat-offline"] = "Скачайте и запустите античит GameWer - http://vk.com/allowerd",
				["gamewer-alreadyDetected"] = "Что то пошло не так, перезапустите античит!",
				["gamewer-closed"] = "Связь с античитом на вашей стороне - потеряна, перезапустите античит!",
			}, this, "ru");
		}

		private string _(ulong usetID, string key, params object[] args)
		{
			return string.Format(lang.GetMessage(key, this, usetID.ToString()), args);
		}

		#endregion

		#region Configuration

		public class Configuration
		{
			[JsonProperty("Лицензионный ключ GameWer")]
			public string LicensKey = "licensKey";

			[JsonProperty("Принудительно включить GameWer для игроков с лицензией")]
			public bool NeedGameWerFromLicens = false;

			[JsonProperty("SteamID владелеца сервера")]
			public ulong OwnerID = 0;

			[JsonProperty("SteamID модераторов сервера")]
			public List<ulong> Assists = new List<ulong>();

			[JsonProperty("Проверять игроков с GameWer на использование VPN")]
			public bool CheckVPN = true;

			[JsonProperty("Список заблокированных игроков по HWID")]
			public List<string> BannedMachineID = new List<string>();

			[JsonProperty("Список SteamID игроков которым обязателен запуск GameWer")]
			public List<ulong> TargetSteamID = new List<ulong>();

			[JsonProperty("Пускать игроков даже если у них бан в GameWer")]
			public bool IgnoreBanned = false;

			[JsonProperty("Список SteamID которых пускать, даже если есть бан GameWer")]
			public List<ulong> WhiteList = new List<ulong>();

			[JsonProperty("Список SteamID которым можно играть без GameWer")]
			public List<ulong> IgnoreList = new List<ulong>();

			[JsonProperty("Подробная информация в консоль сервера?")]
			public bool DebugMod = false;
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				this._config = Config.ReadObject<Configuration>();
			}
			catch
			{
				PrintError("Error reading config, please check!");
			}
		}

		protected override void LoadDefaultConfig()
		{
			Config.WriteObject(new Configuration(), true);
		}

		#endregion

		#region WSH

		public class WSH
		{
			public WebSocket SocketClient = new WebSocket("ws://auth.server.gamewer.ru:9912/");

			public bool HasUnloaded = false;
			public bool NetworkStatus = false;
			public bool AuthStatus = false;
			public uint SessionID = 0;

			public WSH()
			{
				SocketClient.OnMessage += OnSocketMessage;
				SocketClient.OnOpen += OnSocketConnected;
				SocketClient.OnClose += OnSocketDisconnected;
			}

			#region Public Functions

			public void Connect()
			{
				if (!this.HasUnloaded)
				{
					_instance.PrintWarning("[WS] Trying to establish a connection to the GameWer server...");
					SocketClient.ConnectAsync();
				}
			}

			public void Send(Dictionary<string, object> packet, bool ignoreAuth = false)
			{
				if (this.AuthStatus || ignoreAuth)
				{
					string packPacket = JsonConvert.SerializeObject(packet);
#if DEBUG
					_instance.PrintWarning($"[DEBUG][WS][<-----]: {packPacket}");
#endif
					SocketClient.SendAsync(packPacket, status => { });
				}
			}

			public void Close()
			{
				this.HasUnloaded = true;
				SocketClient.CloseAsync();
				_instance._authList.Clear();
			}

			#endregion

			#region Handler Functions

			private void OnSocketConnected(object sender, EventArgs e)
			{
				_instance.PrintWarning("[WS] Socket Connected...");

				this.NetworkStatus = true;
				this.SessionID = this.SessionID + 1;
				string hostkey = "";

				_instance.PrintWarning("[WS] Start AntiCheat auth from key: " + _instance._config.LicensKey);

				this.Send(new Dictionary<string, object>
				{
					{"method", "server"},
					{"license_key", (string.IsNullOrEmpty(hostkey) ? _instance._config.LicensKey : hostkey)},
					{"temporary_key", (string.IsNullOrEmpty(hostkey) ? "" : _instance._config.LicensKey)},
					{"hostname", ConVar.Server.hostname},
				}, true);
			}

			private void OnSocketMessage(object sender, MessageEventArgs e)
			{
#if DEBUG
				_instance.PrintWarning($"[DEBUG][WS][----->]: {e.Data}");
#endif
				_instance.NextFrame(() =>
				{
					try
					{
						Dictionary<string, object> json = JsonConvert.DeserializeObject<Dictionary<string, object>>(e.Data);
						if (json.ContainsKey("method"))
						{
							switch ((string) json["method"])
							{
								case "setTemporaryKey":
									this.OnTemporaryKey(json);
									break;

								case "kickPlayer":
									this.OnKickPlayer(json);
									break;

								case "authResult":
									this.OnAuthResult(json);
									break;

								case "setMachineID":
									this.OnTakeMachineID(json);
									break;

								default:
									_instance.PrintError($"[WS] Undefined method: {json["method"]}");
									break;
							}
						}
					}
					catch (Exception ex)
					{
						_instance.PrintError($"Exception from OnSocketMessage: {ex}");
					}
				});
			}

			private void OnSocketDisconnected(object sender, CloseEventArgs e)
			{
				if (string.IsNullOrEmpty(e.Reason))
					_instance.PrintWarning("[WS] Socket Disconnected.");
				else
					_instance.PrintError($"[WS] Socket Disconnected: {e.Reason}");

				this.NetworkStatus = false;
				this.AuthStatus = false;

				if (!this.HasUnloaded)
				{
					_instance.timer.Once(10, Connect);
				}
			}

			private void OnAuthResult(Dictionary<string, object> json)
			{
				if (json.ContainsKey("result") && json["result"] is Boolean)
				{
					_instance.PrintWarning("[WS] Auth result: " + json["result"] + "!");
					if ((bool) json["result"] == true)
					{
						this.AuthStatus = true;
						for (var i = 0; i < BasePlayer.activePlayerList.Count; i++)
						{
							if (BasePlayer.activePlayerList[i].Connection.userid != 0)
							{
								_instance.DoAuthPlayer(BasePlayer.activePlayerList[i].Connection);
							}
						}

						return;
					}
				}

				_instance._wsh.SocketClient.CloseAsync(CloseStatusCode.Normal);
			}

			private void OnKickPlayer(Dictionary<string, object> json)
			{
				try
				{
					if (json.ContainsKey("steamid") && json.ContainsKey("reason"))
					{
						_instance.DebugModLog("Need kick packet, steamid: " + json["steamid"] + "; reason: " + json["reason"]);

						ulong steamid = 0;
						Connection connection = null;
						if (ulong.TryParse((string) json["steamid"], out steamid))
						{
							connection = Network.Net.sv.connections.FirstOrDefault((Connection x) => x.userid == steamid);
							if (connection != null)
							{
								if (_instance._authList.Contains(connection))
								{
									_instance._authList.Remove(connection);
								}

								string reason = _instance._(connection.userid, (string) json["reason"]);
								_instance.Puts($"GameWer kick player [{connection.userid} / {connection.username}] from reason: {reason}");
								Network.Net.sv.Kick(connection, $"GameWer: {reason}");
							}
							else
							{
								_instance.DebugModLog("Not found connection: " + json["steamid"] + " to kick");
							}
						}

						connection = _instance._authList.FirstOrDefault((Connection x) => x.userid == steamid);
						if (connection != null)
						{
							_instance._authList.Remove(connection);

							string reason = _instance._(connection.userid, (string) json["reason"]);
							Network.Net.sv.Kick(connection, $"GameWer: {reason}");
						}
					}
				}
				catch (Exception ex)
				{
					_instance.Puts("Exception in OnKickPlayer: " + ex);
				}
			}

			private void OnTakeMachineID(Dictionary<string, object> json)
			{
				if (json.ContainsKey("steamid") && json.ContainsKey("machineID"))
				{
					_instance.DebugModLog("Take machineID packet, steamid: " + json["steamid"] + "; machineID: " + json["machineID"]);
					ulong steamid = 0;
					if (ulong.TryParse((string) json["steamid"], out steamid))
					{
						_instance._machineIDList[steamid] = (string) json["machineID"];
						Connection connection = Network.Net.sv.connections.FirstOrDefault((Connection x) => x.userid == steamid);
						if (connection != null)
						{
							if (_instance._config.BannedMachineID.Contains((string) json["machineID"]))
							{
								_instance.Puts($"GameWer banned player [{connection.userid} / {connection.username}] from HWID!");
								Network.Net.sv.Kick(connection, "You banned from this server!");

								return;
							}

							_instance.Puts($"Auth success player [{connection.userid} / {connection.username}] from HWID: {json["machineID"]}");
							uint sessionID = this.SessionID;
							_instance.timer.Once(60, () => this.DoScreen(connection, sessionID));
						}
						else
						{
							_instance.DebugModLog("Not found connection: " + json["steamid"] + " to getMachineID");
						}
					}
				}
			}

			public void DoScreen(Connection connection, uint sessionID)
			{
				if (this.AuthStatus && sessionID == this.SessionID && connection.guid != 0 && _instance._authList.Contains(connection))
				{
					_instance.DebugModLog("Player [" + connection.userid + " / " + connection.username + "] send getScreen to GameWerServer");
					this.Send(new Dictionary<string, object>
					{
						{"method", "getScreen"},
						{"steamid", connection.userid.ToString()},
					});

					sessionID = this.SessionID;
					_instance.timer.Once(UnityEngine.Random.Range(500, 1500), () => this.DoScreen(connection, sessionID));
				}
			}

			private void OnTemporaryKey(Dictionary<string, object> json)
			{
				if (json.ContainsKey("temporaryKey"))
				{
					_instance._config.LicensKey = (string) json["temporaryKey"];
					_instance.Config.WriteObject(_instance._config, true);

					_instance.setServerOwner();
				}
			}

			#endregion
		}

		#endregion

		#region Init

		private void Init()
		{
			_instance = this;
			_wsh = new WSH();

		}

		private void OnServerInitialized()
		{
			this.setServerOwner();
			_wsh.Connect();

			this.timer.Repeat(1, 0, () =>
			{
				if (this._hasUnloaded == false)
				{
					for (var i = this._authList.Count - 1; i >= 0; i--)
					{
						if (this._authList[i].guid == 0)
						{
							if (_wsh.AuthStatus)
							{
								_wsh.Send(new Dictionary<string, object>
								{
									{"method", "leavePlayer"},
									{"steamid", this._authList[i].userid.ToString()},
								});
							}

							this._authList.Remove(this._authList[i]);
						}
					}


				}
			});
		}

		private void Unload()
		{
			_wsh.Close();
			_hasUnloaded = true;
		}

		#endregion

		#region Oxide Hooks


		void OnPlayerConnected(BasePlayer player)
		{
			this.DoAuthPlayer(player.Connection);
		}

		[ChatCommand("gw.screen")]
		private void CmdGWScreen(BasePlayer player, string command, string[] args)
		{
			if (player.Connection.authLevel == 0)
				return;
			if (args.Length == 0)
				return;

			string findLine = args[0].Trim().ToLower();
			BasePlayer target = BasePlayer.activePlayerList.FirstOrDefault(p => p.displayName.ToLower().Contains(findLine) || p.UserIDString.Contains(findLine));

			if (target != null)
			{
				if (_wsh.AuthStatus)
				{
					_instance.DebugModLog("Player [" + target.UserIDString + " / " + target.displayName + "] send getScreen to GameWerServer");
					_wsh.Send(new Dictionary<string, object>
					{
						{"method", "getScreen"},
						{"steamid", target.UserIDString},
					});
				}
			}
			else
			{
				player.ChatMessage("<color=orange>[GameWer]</color>: Player not found");
			}
		}

		[ChatCommand("gw.ban")]
		private void CmdGWBan(BasePlayer player, string command, string[] args)
		{
			if (player.Connection.authLevel == 0)
				return;
			if (args.Length == 0)
				return;


			string findLine = args[0].Trim().ToLower();
			BasePlayer target = BasePlayer.activePlayerList.FirstOrDefault(p => p.displayName.ToLower().Contains(findLine) || p.UserIDString.Contains(findLine));
			if (target != null)
			{
				if (_machineIDList.ContainsKey(target.userID))
				{
					string machineid = _machineIDList[target.userID];
					if (_config.BannedMachineID.Contains(machineid) == false)
					{
						target.Kick("You banned to this server!");
						player.ChatMessage($"<color=orange>[GameWer]</color>: Player [{target.displayName}] added to banlist");
						_config.BannedMachineID.Add(machineid);
						Config.WriteObject(_config, true);
					}
					else
					{
						player.ChatMessage($"<color=orange>[GameWer]</color>: Player [{target.displayName}] is banlist");
					}
				}
				else
				{
					player.ChatMessage("<color=orange>[GameWer]</color>: Not found machineID player, player no use GameWer to Current Session");
				}
			}
			else
			{
				player.ChatMessage("<color=orange>[GameWer]</color>: Player not found");
			}
		}

		[ChatCommand("gw.whitelist")]
		private void CmdGWWhitelist(BasePlayer player, string command, string[] args)
		{
			if (player.Connection.authLevel == 0)
				return;
			if (args.Length == 0)
				return;


			string steamidSTR = args[0].Trim().ToLower();
			ulong steamid = 0;

			if (steamidSTR.Length == 17 && ulong.TryParse(steamidSTR, out steamid))
			{
				if (_config.WhiteList.Contains(steamid))
				{
					_config.WhiteList.Remove(steamid);
					player.ChatMessage($"<color=orange>[GameWer]</color>: Player [{steamidSTR}] added to WhiteList");
				}
				else
				{
					_config.WhiteList.Add(steamid);
					player.ChatMessage($"<color=orange>[GameWer]</color>: Player [{steamidSTR}] removed from WhiteList");
				}

				Config.WriteObject(_config, true);
			}
			else
			{
				player.ChatMessage($"<color=orange>[GameWer]</color>: You write steamid [{steamidSTR}] is not correct!");
			}
		}

		[ChatCommand("gw.ignore")]
		private void CmdGWIgnore(BasePlayer player, string command, string[] args)
		{
			if (player.Connection.authLevel == 0)
				return;
			if (args.Length == 0)
				return;


			string steamidSTR = args[0].Trim().ToLower();
			ulong steamid = 0;

			if (steamidSTR.Length == 17 && ulong.TryParse(steamidSTR, out steamid))
			{
				if (_config.IgnoreList.Contains(steamid))
				{
					_config.IgnoreList.Remove(steamid);
					player.ChatMessage($"<color=orange>[GameWer]</color>: Player [{steamidSTR}] added to IgnoreList");
				}
				else
				{
					_config.IgnoreList.Add(steamid);
					player.ChatMessage($"<color=orange>[GameWer]</color>: Player [{steamidSTR}] removed from IgnoreList");
				}

				Config.WriteObject(_config, true);
			}
			else
			{
				player.ChatMessage($"<color=orange>[GameWer]</color>: You write steamid [{steamidSTR}] is not correct!");
			}
		}

		[ChatCommand("gw.need")]
		private void CmdGWNeed(BasePlayer player, string command, string[] args)
		{
			if (player.Connection.authLevel == 0)
				return;
			if (args.Length == 0)
				return;


			string steamidSTR = args[0].Trim().ToLower();
			ulong steamid = 0;

			if (steamidSTR.Length == 17 && ulong.TryParse(steamidSTR, out steamid))
			{
				BasePlayer target = BasePlayer.activePlayerList.FirstOrDefault(p => p.userID == steamid);
				if (target != null && target.Connection != null && target.Connection.ownerid != 0)
				{
					steamid = target.Connection.ownerid;
				}

				if (_config.TargetSteamID.Contains(steamid))
				{
					_config.TargetSteamID.Remove(steamid);
					player.ChatMessage("<color=orange>[GameWer]</color>: Player [" + steamid + "] added to NeedList");
				}
				else
				{
					_config.TargetSteamID.Add(steamid);
					player.ChatMessage("<color=orange>[GameWer]</color>: Player [" + steamid + "] removed from NeedList");
				}

				Config.WriteObject(_config, true);
			}
			else
			{
				player.ChatMessage($"<color=orange>[GameWer]</color>: You write steamid [{steamidSTR}] is not correct!");
			}
		}

		[ChatCommand("gw.reload")]
		private void CmdGWReload(BasePlayer player, string command, string[] args)
		{
			if (player.Connection.authLevel == 0)
				return;
			_wsh.SocketClient.CloseAsync();
			_instance._authList.Clear();
			player.ChatMessage($"<color=orange>[GameWer]</color>: Start reloading...!");
		}

		[ChatCommand("gw.help")]
		private void CmdGWHelp(BasePlayer player, string command, string[] args)
		{
			if (player.Connection.authLevel == 0)
				return;
			player.ChatMessage($"<color=orange>[GameWer]</color>: Commands:\ngw.reload - Reload connection\ngw.need [STEAMID] - Player need use GameWer\ngw.whitelist [STEAMID] - Player ignore banlist\ngw.ignore [STEAMID] - Player not need run GameWer\ngw.ban [STEAMID or USERNAME] - Add machineID to ban list(if player online)\ngw.screen [STEAMID] - request new screen");
		}

		#endregion

		#region Helpers

		private bool CanNeedGW(Connection connection)
		{
			try
			{
				if (connection.guid == 0 || _authList.Contains(connection) == true)
				{
					return false;
				}

				if (this._config.IgnoreList.Contains(connection.userid))
				{
					_instance.DebugModLog("Player [" + connection.userid + " / " + connection.username + "] in ignore list");
					return false;
				}

				if (this._config.NeedGameWerFromLicens)
					return true;

				if (this._config.TargetSteamID.Contains(connection.userid) || this._config.TargetSteamID.Contains(connection.ownerid))
					return true;

				_instance.DebugModLog("Player [" + connection.userid + " / " + connection.username + "] tokenLen: " + connection.token.Length);
				if (connection.token.Length == 0x000000EA || connection.token.Length == 0x000000F0)
				{
					uint offsetUnsignedInteger = BitConverter.ToUInt32(connection.token, 0x00000048);
					_instance.DebugModLog("Player [" + connection.userid + " / " + connection.username + "] tokenID: " + offsetUnsignedInteger);
					if (offsetUnsignedInteger == 0x000001E0)
					{
						_instance.DebugModLog("Player [" + connection.userid + " / " + connection.username + "] is Pirate");
						return true;
					}
					else
					{
						_instance.DebugModLog("Player [" + connection.userid + " / " + connection.username + "] maybe is License");
					}
				}
			}
			catch (Exception ex)
			{
				this.PrintError("Exception in CanNeedGW(" + connection.userid + "): " + ex);
			}

			return false;
		}

		private void DebugModLog(string text)
		{
			if (this._config.DebugMod == true)
			{
				this.Puts("[GameWer DEBUG]: " + text);
			}
		}

		private void DoAuthPlayer(Connection connection)
		{
			if (_wsh.AuthStatus && this._hasUnloaded == false)
			{
				if (this.CanNeedGW(connection))
				{
					if (connection.guid != 0 && this._authList.Contains(connection) == false)
					{
						this._authList.Add(connection);
						_instance.DebugModLog("Player [" + connection.userid + " / " + connection.username + "] send joinPlayer to GameWerServer");
						_wsh.Send(new Dictionary<string, object>
						{
							{"method", "joinPlayer"},
							{"steamid", connection.userid.ToString()},
							{"ip", ((this._config.CheckVPN) ? connection.ipaddress.Split(':')[0] : "0.0.0.0")},
							{"isIgnoreBanned", (this._config.IgnoreBanned || this._config.WhiteList.Contains(connection.userid))}
						});

						_instance.DebugModLog("Player [" + connection.userid + " / " + connection.username + "] send getHWD to GameWerServer");
						_wsh.Send(new Dictionary<string, object>
						{
							{"method", "getHWID"},
							{"steamid", connection.userid.ToString()},
						});
					}
				}
				else
					Puts($"Not need auth player [{connection.userid} / {connection.username}]");
			}
			else
			{
				_instance.DebugModLog("Player [" + connection.userid + " / " + connection.username + "] not auth, GameWerServer not auth did this server!");
			}
		}

		private void setServerOwner()
		{
			string lineJoin = "";
			for (var i = 0; i < this._config.Assists.Count; i++)
			{
				lineJoin += (lineJoin.Length != 0 ? "," : "") + this._config.Assists[i];
			}

			this.webrequest.Enqueue($"http://server.gamewer.ru:61080/?/api/api.SetServerOwner&licensKey={this._config.LicensKey}&ownerid={this._config.OwnerID}&assists={lineJoin}", "", (code, response) => { }, this, RequestMethod.GET);
		}

		#endregion
	}
}